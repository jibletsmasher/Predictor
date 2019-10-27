using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Predictor
{
    class Control
    {
        private static string filePath = @"C:\Projects\Predictor\Predictor\Predictor\TGT max.csv";
        public static bool isTesting = true;
        public static bool isShort = false;
        public static string GetFilePath()
        {
            //return @"C:\Projects\Predictor\Predictor\Predictor\GS.csv";
            return filePath;
        }

        public static void Run(bool isShort)
        {
            string[] files = Directory.GetFiles(@"C:\Projects\Predictor\Symbols");
            for (int i=0; i < files.Length; i++)
            {
                filePath = files[i];
                DateTime dateToSell = Program.Run(isShort);
                if (dateToSell.Ticks == new DateTime().Ticks)
                {
                    continue;
                }
                else if (dateToSell.Ticks < DateTime.Now.Ticks || dateToSell.Ticks > (DateTime.Now.Ticks + new TimeSpan(182, 0, 0, 0, 0).Ticks))
                {

                }
                else
                {
                    Debug.WriteLine("Buy " + files[i] + " now!  Sell on " + dateToSell.ToString());
                }
            }
        }

        public static void Test(bool isShort)
        {
            using (var reader = new StreamReader(GetFilePath()))
            {
                // Read the first line.
                reader.ReadLine();

                int totalCount = 0;
                int successCount = 0;
                int failCount = 0;
                double gain = 0;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    var dateValues = values[0].Split('-');

                    // DateTime constructor (year, month, day)
                    DateTime dateStart = new DateTime(Convert.ToInt16(dateValues[0]), Convert.ToInt16(dateValues[1]), Convert.ToInt16(dateValues[2]));
                    Debug.WriteLine("This is start date " + dateStart.ToString());
                    long timeEnd = dateStart.Ticks + PointOfInterest.GetTimeRange().Ticks;
                    PointOfInterest.SetTimeEnd(timeEnd);

                    DateTime dateToSell = Program.Run(isShort);
                    if (dateToSell.Ticks == new DateTime().Ticks)
                    {
                        continue;
                    }
                    // Remove hours, minutes, and seconds from date information
                    dateToSell = new DateTime(dateToSell.Year, dateToSell.Month, dateToSell.Day);
                    if (dateToSell.Ticks < timeEnd || dateToSell.Ticks > (timeEnd + new TimeSpan(182, 0, 0, 0, 0).Ticks))
                    {
                        Debug.WriteLine(new DateTime(timeEnd).ToString() + " " + dateToSell.ToString());
                        return;
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

                    double lowPrice = Convert.ToDouble(values[1]);

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

                        double highPrice = Convert.ToDouble(values[1]);
                        if (lowPrice < highPrice)
                        {
                            // Fuck yes
                            Debug.WriteLine("Fuck Yes low price: " + lowPrice.ToString() + ".  High price " + highPrice.ToString());
                            totalCount++;
                            successCount++;
                            gain += (highPrice - lowPrice);
                        }
                        else if (lowPrice > highPrice)
                        {
                            // NOOOO
                            Debug.WriteLine("NOOOOOO low price: " + lowPrice.ToString() + ".  High price " + highPrice.ToString());
                            totalCount++;
                            failCount++;
                            gain += (highPrice - lowPrice);
                        }
                    }
                }
                Debug.WriteLine("Success: " + successCount + ".  Total: " + totalCount + ". Gain: " + gain);
            }
        }
    }
}
