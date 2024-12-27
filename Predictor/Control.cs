using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Predictor
{
    class Control
    {
        // Get crypto csv historical data: https://coinmetrics.io/data-downloads/  PriceUSD is 19th column
        // This file path is dynamic when actually running the code (do a ctrl+f to see how)
        private static string filePath = @"C:\Projects\Predictor\Predictor\Predictor\ada.csv"; // For crypto
        private static string testFileName = "GS.csv";
        public static bool isTesting = false;
        public static bool isShort = false;
        public static bool isCrypto = false;

        public static StreamWriter logWriter = null;

        public static int maxDaysToHold = 40; // 50 for real data <- I don't know why we should only hold for 50 days for real data
        public static int minDaysToHold = 32; // 32 for real data
        public static string GetFilePath()
        {
            if (!Control.isTesting)
            {
                return filePath;
            }
            else
            {
                return $"C:\\Projects\\Predictor\\Predictor\\Predictor\\{testFileName}";
            }
        }

        public static void SetupLogWriter()
        {
            if (logWriter == null)
            {
                // Setup log file
                string processFolder = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                var culture = CultureInfo.CreateSpecificCulture("en-US");
                DirectoryInfo directory = Directory.CreateDirectory(processFolder + "\\LogFiles");
                string shortLong = Control.isShort ? "short" : "long";
                string testing = Control.isTesting ? $"-testing-{testFileName}" : "";
                string pathPlacehodler = directory.FullName + "\\" + DateTime.Now.Date.ToString("d", culture).Replace('/', '-') + "-" + shortLong + testing + ".txt";
                logWriter = new StreamWriter(pathPlacehodler, false);
            }
        }

        public static void CloseLogWriter()
        {
            logWriter.Close();
        }

        public static void Run(bool isShort)
        {
            SetupLogWriter();

            List<string> tickersToBuy = new List<string>();
            string[] files = Directory.GetFiles(@"C:\Projects\Predictor\Symbols");
            for (int i=0; i < files.Length; i++)
            {
                if (files[i].Contains(".RData") || files[i].Contains(".Rhistory")) continue;
                filePath = files[i];
                DateTime dateToSell = Program.Run(isShort);
                if (dateToSell.Ticks == new DateTime().Ticks)
                {
                    continue;
                }
                else if (dateToSell.Ticks < DateTime.Now.Ticks ||
                    dateToSell.Ticks > (DateTime.Now.Ticks + new TimeSpan(maxDaysToHold, 0, 0, 0, 0).Ticks) ||
                    dateToSell.Ticks < (DateTime.Now.Ticks + new TimeSpan(minDaysToHold, 0, 0, 0, 0).Ticks))
                {

                }
                else
                {
                    var fileParts = files[i].Split(new char[] { '\\' });
                    var ticker = fileParts[4].Split(new char[] { ' ' })[0];
                    tickersToBuy.Add(ticker);
                    var info = "Buy " + files[i] + " now!  Sell on " + dateToSell.ToString();
                    Debug.WriteLine(info);
                    logWriter.WriteLine(info);
                }
            }
            foreach(var ticker in tickersToBuy)
            {
                logWriter.Write($"\"{ticker}\",");
            }
        }

        public static void Test(bool isShort)
        {
            SetupLogWriter();
            using (var reader = new StreamReader(GetFilePath()))
            {
                // Read the first line.
                reader.ReadLine();

                double totalSuccessDays = 0,totalFailureDays = 0;
                bool additionalInfo = false;
                int totalCount = 0;
                int successCount = 0;
                int failCount = 0;
                double gain = 0;
                double funds = 1d;
                int index = Control.isCrypto ? 18 : 1;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    var dateValues = values[0].Split('-');

                    // DateTime constructor (year, month, day)
                    DateTime dateStart = new DateTime(Convert.ToInt16(dateValues[0]), Convert.ToInt16(dateValues[1]), Convert.ToInt16(dateValues[2]));
                    long timeEnd = dateStart.Ticks + PointOfInterest.GetTimeRange().Ticks;
                    PointOfInterest.SetTimeEnd(timeEnd);

                    DateTime dateToSell = Program.Run(isShort);
                    if (dateToSell.Ticks == new DateTime().Ticks)
                    {
                        // There was no dateToSell determined.
                        continue;
                    }
                    // Remove hours, minutes, and seconds from date information
                    dateToSell = new DateTime(dateToSell.Year, dateToSell.Month, dateToSell.Day);
                    if (dateToSell.Ticks < timeEnd)
                    {
                        // The dateToSell is before the "present" day or it is too far out in the future.
                        // TODO: run tests to determine what is actually "too far" in the future.  Currently at 6 months.
                        continue;
                    }
                    else if (dateToSell.Ticks > (timeEnd + new TimeSpan(maxDaysToHold, 0, 0, 0, 0).Ticks) || dateToSell.Ticks < (timeEnd + new TimeSpan(minDaysToHold, 0, 0, 0, 0).Ticks))
                    {
                        //additionalInfo = true;
                        continue;
                    }

                    // Get the date that you should buy the stock (i.e. "today")
                    DateTime dateCheck = dateStart;
                    using (var thirdReader = new StreamReader(GetFilePath()))
                    {
                        line = thirdReader.ReadLine();
                        while (dateCheck.Ticks < PointOfInterest.GetTimeEnd().Ticks && !thirdReader.EndOfStream)
                        {
                            line = thirdReader.ReadLine();
                            values = line.Split(',');
                            dateValues = values[0].Split('-');

                            // DateTime constructor (year, month, day)
                            dateCheck = new DateTime(Convert.ToInt16(dateValues[0]), Convert.ToInt16(dateValues[1]), Convert.ToInt16(dateValues[2]));
                        }
                    }

                    DateTime buyDate = new DateTime(dateCheck.Ticks);
                    // Get the price of the buy day
                    double buyPrice = Convert.ToDouble(values[index]);

                    // Get the date you should sell
                    using (var secondReader = new StreamReader(GetFilePath()))
                    {
                        // Read the first line.
                        secondReader.ReadLine();

                        while (dateCheck.Ticks < dateToSell.Ticks && !secondReader.EndOfStream)
                        {
                            line = secondReader.ReadLine();
                            values = line.Split(',');
                            dateValues = values[0].Split('-');

                            // DateTime constructor (year, month, day)
                            dateCheck = new DateTime(Convert.ToInt16(dateValues[0]), Convert.ToInt16(dateValues[1]), Convert.ToInt16(dateValues[2]));
                        }
                        // Get the price of the sell day
                        double sellPrice = Convert.ToDouble(values[index]);
                        if (buyPrice < sellPrice)
                        {
                            // Fuck yes
                            Debug.WriteLine("Fuck Yes buy price: " + buyPrice.ToString() + ".  Sell price " + sellPrice.ToString());
                            logWriter.WriteLine("Fuck Yes buy price: " + buyPrice.ToString() + ".  Sell price " + sellPrice.ToString() + "  Sell Date: " + dateToSell.ToString("MM/dd/yyyy"));
                            totalSuccessDays += (dateToSell.Ticks - timeEnd) / TimeSpan.TicksPerDay;
                            totalCount++;
                            successCount++;
                            
                            gain += (sellPrice - buyPrice);
                        }
                        else if (buyPrice > sellPrice)
                        {
                            // NOOOO
                            Debug.WriteLine("NOOOOOO buy price: " + buyPrice.ToString() + ".  Sell price " + sellPrice.ToString());
                            logWriter.WriteLine("NOOOOOO buy price: " + buyPrice.ToString() + ".  Sell price " + sellPrice.ToString() + ";  Buy Date: " + buyDate.ToString("MM/dd/yyyy") + ";  Sell Date: " + dateToSell.ToString("MM/dd/yyyy"));
                            totalFailureDays += (dateToSell.Ticks - timeEnd) / TimeSpan.TicksPerDay;
                            totalCount++;
                            failCount++;
                            gain += (sellPrice - buyPrice);
                        }
                        var numOfShares = funds / buyPrice;
                        if (buyPrice - buyPrice * .05 > sellPrice) sellPrice = buyPrice - buyPrice * 0.05;
                        funds = numOfShares * sellPrice;
                        if (additionalInfo)
                        {
                            Debug.WriteLine("See above period of time prices for a holding range of: " + new DateTime(dateToSell.Ticks - timeEnd).Ticks/TimeSpan.TicksPerDay);
                            additionalInfo = false;
                        }
                    }
                }
                var infos = new string[] {
                    "Success: " + successCount + ".  Total: " + totalCount + ". Gain: " + gain,
                    "Average days for success: " + (totalSuccessDays / successCount).ToString(),
                    "Average days for failure: " + (totalFailureDays / failCount).ToString(),
                    "Total funds leftover: " + funds.ToString()
                };
                foreach (var info in infos)
                {
                    Debug.WriteLine(info);
                    logWriter.WriteLine(info);
                }
            }
        }
    }
}
