using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Predictor
{
    struct DataPoint
    {
        public DateTime date;
        // For stocks:
        //  previousValue - open
        //  currentValue - close
        //  nextValue - next open
        public double previousValue;
        public double currentValue;
        public double nextValue;

        public DataPoint(DateTime date, double previousValue, double currentValue, double nextValue)
        {
            this.date = date;
            this.previousValue = previousValue;
            this.currentValue = currentValue;
            this.nextValue = nextValue;
        }
    }

    struct PointOfInterest
    {
        public static bool isShort;
        public static bool isTesting;
        public static bool isCrypto;
        private static DateTime timeEnd = isTesting ? new DateTime(2005, 3, 1) : DateTime.Now;
        private static TimeSpan timeRange = new TimeSpan(90, 0, 0, 0);
        private static DateTime timeStart = new DateTime(timeEnd.Ticks - timeRange.Ticks);
        private DateTime date;
        public double value;
        public int daysFromTimeStart;

        public PointOfInterest(DateTime date, double value)
        {
            this.date = date;
            this.value = value;

            this.daysFromTimeStart = (int)((date.Ticks - timeStart.Ticks) / TimeSpan.TicksPerDay);
        }

        public static DateTime GetTimeStart()
        {
            return timeStart;
        }

        public static DateTime GetTimeEnd()
        {
            return timeEnd;
        }

        public static void SetTimeEnd(long ticks)
        {
            timeEnd = new DateTime(ticks);
            timeStart = new DateTime(timeEnd.Ticks - timeRange.Ticks);
        }

        public static TimeSpan GetTimeRange()
        {
            return timeRange;
        }
    }

    struct BestFitLine
    {
        public double slope;
        public double yIntercept;

        public BestFitLine(double slope, double yIntercept)
        {
            this.slope = slope;
            this.yIntercept = yIntercept;
        }
    }

    class Program
    {
        // Make this smaller to be less restrictive and larger to be more restrictive of what we consider to be a point of interest.
        // If you are bullish on a company, then set this to PI/4.  If bearish, then PI/3.
        public static double rotation = Math.PI / 3;

        static void Main(string[] args)
        {
            PointOfInterest.isTesting = Control.isTesting;
            PointOfInterest.isShort = Control.isShort;
            PointOfInterest.isCrypto = Control.isCrypto;
            if (PointOfInterest.isTesting)
            {
                Control.Test(PointOfInterest.isShort);
            }
            else
            {
                Control.Run(PointOfInterest.isShort);
            }
        }

        public static DateTime Run(bool isShort)
        {
            List<DataPoint> dataPoints = GetDataPoints();

            // Used to calculate mean x and y values for best fit lines
            int highXValueTotalDays = 0;
            double highYValueTotal = 0;
            int lowXValueTotalDays = 0;
            double lowYValueTotal = 0;
            List<PointOfInterest>[] pointsOfInterest = GetPointsOfInterest(dataPoints, ref highXValueTotalDays, ref highYValueTotal, ref lowXValueTotalDays, ref lowYValueTotal);
            List<PointOfInterest> lowValues = pointsOfInterest[0];
            List<PointOfInterest> highValues = pointsOfInterest[1];

            if (lowValues.Count == 0 || highValues.Count == 0)
            {
                //Debug.WriteLine("There are either no low values or no high values of interest.");
                return new DateTime();
            }

            BestFitLine highBestFit = GetBestFit(highValues, highXValueTotalDays, highYValueTotal);
            BestFitLine lowBestFit = GetBestFit(lowValues, lowXValueTotalDays, lowYValueTotal);

            // If we're in a long position and the high slope is greater than 0, then we can't determine
            // what will happen given the relationship between the high and low value slopes.
            // Likewise, if we're in a short position and the low slope is less than 0, then we can't determine
            // what will happen given the relationship between the high and low value slopes.
            if (!isShort && highBestFit.slope > 0 || isShort && lowBestFit.slope < 0)
            {
                //Debug.WriteLine("Best fit " + ((!isShort && highBestFit.slope > 0) ? "high slope is greater than 0." : "low slope is less than 0."));
                return new DateTime();
            }

            // If we're in a short position and expect a decrease, then we want to find the date to sell.
            // Likewise, if we're in a long position and expect an increase, then we want to find the date to sell.
            bool willIncrease = highBestFit.slope < lowBestFit.slope ? true : false;
            if (isShort && !willIncrease || !isShort && willIncrease)
            {
                // A falling wedge is occurring, so calculate when lines intersect to determine when to sell.
                double xCoefficient = highBestFit.slope - lowBestFit.slope;
                double interceptConstant = lowBestFit.yIntercept - highBestFit.yIntercept;
                // xIntersection is the approximate time stamp that the stock should be sold at to make a profit.
                double xIntersection = interceptConstant / xCoefficient;

                // Convert xIntersection Ticks to DateTime
                long ticks = (long)xIntersection * TimeSpan.TicksPerDay + PointOfInterest.GetTimeStart().Ticks;
                DateTime dateToSell = new DateTime(ticks);
                return dateToSell;
            }
            //Debug.WriteLine("Either we want a short position and it will increase or we want a long position and it will decrease");
            return new DateTime();
        }

        // Retrieves all data points within the time range to be wittled down to points of interest later.
        public static List<DataPoint> GetDataPoints()
        {
            // Retrieving historical data:
            // https://finance.yahoo.com/quote/TGT/history?p=TGT
            List<DataPoint> dataPoints = new List<DataPoint>();
            using (var reader = new StreamReader(Control.GetFilePath()))
            {
                double nextValue = 0;
                double previousValue = 0;
                double currentValue = 0;

                // Read the first line.
                reader.ReadLine();
                var previousLine = reader.ReadLine();
                var previousValues = previousLine.Split(',');
                var currentLine = reader.ReadLine();
                var currentValues = currentLine.Split(',');

                int index = 0;
                int offset = PointOfInterest.isCrypto ? 18 : 4; // stocks close value is index 4, while crypo's usd price value is index 18
                string[] dateValues;
                if (!PointOfInterest.isTesting)
                {
                    index = 1;
                    dateValues = currentValues[index].Split('-');
                    dateValues[0] = dateValues[0].Replace("\"","");
                    dateValues[2] = dateValues[2].Replace("\"", "");
                    for (int i = 0; i < currentValues.Length; i++)
                    {
                        currentValues[i] = currentValues[i].Replace("\"", "");
                    }
                }
                else
                {
                    dateValues = currentValues[index].Split('-');
                }

                while (!reader.EndOfStream)
                {
                    // DateTime constructor (year, month, day)
                    DateTime date = new DateTime(Convert.ToInt16(dateValues[0]), Convert.ToInt16(dateValues[1]), Convert.ToInt16(dateValues[2]));

                    previousValue = Convert.ToDouble(previousValues[index + offset]);
                    currentValue = Convert.ToDouble(currentValues[index + offset]);

                    var nextLine = reader.ReadLine();
                    var nextValues = nextLine.Split(',');
                    nextValue = Convert.ToDouble(nextValues[index + offset].Replace("\"", ""));

                    // Only add a new data point if it is within the desired range.
                    if (PointOfInterest.GetTimeStart().Ticks < date.Ticks && date.Ticks < PointOfInterest.GetTimeEnd().Ticks)
                    {
                        dataPoints.Add(new DataPoint(date, previousValue, currentValue, nextValue));
                    }

                    dateValues = nextValues[index].Split('-');
                    previousValues = currentValues;
                    currentValues = nextValues;
                    if (!PointOfInterest.isTesting)
                    {
                        dateValues[0] = dateValues[0].Replace("\"", "");
                        dateValues[2] = dateValues[2].Replace("\"", "");
                        for (int i=0; i < currentValues.Length; i++)
                        {
                            currentValues[i] = currentValues[i].Replace("\"", "");
                        }
                    }
                }
            }

            return dataPoints;
        }

        public static List<PointOfInterest>[] GetPointsOfInterest(List<DataPoint> dataPoints, ref int highXValueTotal, ref double highYValueTotal, ref int lowXValueTotal, ref double lowYValueTotal)
        {
            // Construct highValues and lowValues arrays with significant points of interest.
            List<PointOfInterest> highValues = new List<PointOfInterest>();
            List<PointOfInterest> lowValues = new List<PointOfInterest>();
            foreach (DataPoint dataPoint in dataPoints)
            {
                double currentValue = dataPoint.currentValue;
                double previousValue = dataPoint.previousValue;
                double nextValue = dataPoint.nextValue;

                // Only calculating rise because run will always be 1
                double previousValueSlope = currentValue - previousValue;
                double nextValueSlope = nextValue - currentValue;

                bool isHighValue = nextValueSlope < previousValueSlope;
                if (isHighValue)
                {
                    rotation = -1 * rotation;
                }

                double slopeThreshold = (Math.Sin(rotation) + previousValueSlope * Math.Cos(rotation)) / (Math.Cos(rotation) - previousValueSlope * Math.Sin(rotation));
                bool isLowerThanThreshold = nextValueSlope < slopeThreshold;

                if (isHighValue && isLowerThanThreshold)
                {
                    PointOfInterest newPOI = new PointOfInterest(dataPoint.date, currentValue);
                    highValues.Add(newPOI);
                    highXValueTotal += newPOI.daysFromTimeStart;
                    highYValueTotal += currentValue;
                }
                else if (!isHighValue && !isLowerThanThreshold)
                {
                    PointOfInterest newPOI = new PointOfInterest(dataPoint.date, currentValue);
                    lowValues.Add(newPOI);
                    lowXValueTotal += newPOI.daysFromTimeStart;
                    lowYValueTotal += currentValue;
                }
            }

            return new List<PointOfInterest>[2] { lowValues, highValues };
        }

        public static BestFitLine GetBestFit(List<PointOfInterest> values, long xValueTotalDays, double yValueTotal)
        {
            // Use highValues and lowValues to determine if a falling wedge is occurring.
            // i.e. is the slope of the high value points less than the slope of the low value points
            // Using best fit line for this:
            double riseBestFit = 0;
            double runBestFit = 0;
            double slope = 0;
            double yIntercept = 0;

            // Calculate high value best fit using least square method
            int xValueMean = (int) xValueTotalDays / values.Count;
            double yValueMean = yValueTotal / values.Count;

            foreach (PointOfInterest pointOfInterest in values)
            {
                int xValueDifference = pointOfInterest.daysFromTimeStart - xValueMean;
                riseBestFit += (xValueDifference) * (pointOfInterest.value - yValueMean);
                runBestFit += (xValueDifference) * (xValueDifference);

                slope = riseBestFit / runBestFit;
                yIntercept = yValueMean - slope * xValueMean;
            }

            return new BestFitLine(slope, yIntercept);
        }

        //public static string GetWebPageSource(string url1, string url2)
        //{
        //    HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(url1);
        //    webrequest.Method = "GET";
        //    webrequest.ContentType = "application/x-www-form-urlencoded";
        //    HttpWebResponse webresponse = (HttpWebResponse)webrequest.GetResponse();

        //    HttpWebRequest webrequest2 = (HttpWebRequest)WebRequest.Create(url2);
        //    webrequest2.Method = "GET";
        //    webrequest2.ContentType = "application/x-www-form-urlencoded";
        //    Uri uri = new Uri("https://query1.finance.yahoo.com");
        //    webrequest2.CookieContainer = new CookieContainer(10);
        //    webrequest2.CookieContainer.Add(new Cookie("APID", "VBd920f336-807c-11e7-8e66-063fe2e383df", "/", ".yahoo.com"));
        //    webrequest2.CookieContainer.Add(new Cookie("APIDTS", "1572149281", "/", ".yahoo.com"));
        //    webrequest2.CookieContainer.Add(new Cookie("B", "fd65bo5cjqqi4&b=3&s=qk", "/", ".yahoo.com"));
        //    webrequest2.CookieContainer.Add(new Cookie("PRF", "t%3DGS", "/", ".finance.yahoo.com"));
        //    webrequest2.CookieContainer.Add(new Cookie("thamba", "2", "/quote/GS", ".finance.yahoo.com"));
        //    HttpWebResponse webresponse2 = (HttpWebResponse)webrequest2.GetResponse();


        //    Encoding enc = Encoding.GetEncoding("utf-8");
        //    StreamReader responseStream = new StreamReader(webresponse2.GetResponseStream(), enc);
        //    string result = string.Empty;
        //    result = responseStream.ReadToEnd();
        //    webresponse.Close();
        //    webresponse2.Close();
        //    return result;
        //}

        //public static string[,] GetSymbolData(string webpageSource)
        //{
        //    int numberOfDays = 90;

        //    int start = webpageSource.IndexOf("data-test=\"historical-prices\"");
        //    string monthAbbreviation = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(DateTime.Now.Month);
        //    int datePosition = webpageSource.IndexOf(monthAbbreviation, start);
        //    if (datePosition == -1)
        //    {
        //        // If today is the first or second and a Saturday or Sunday, then the new month might now be showing on the webpage.
        //        monthAbbreviation = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(DateTime.Now.Month - 1);
        //        datePosition = webpageSource.IndexOf(monthAbbreviation);
        //    }

        //    string[,] symbolData = new string[7, numberOfDays];
        //    int startPosition, endPosition=0;
        //    for (int i=0; i < numberOfDays; i++)
        //    {
        //        symbolData[0, i] = webpageSource.Substring(datePosition, 12);

        //        for (int j=1; j < 7; j++)
        //        {
        //            startPosition = webpageSource.IndexOf("<span", datePosition);
        //            string some2 = webpageSource.Substring(datePosition, 500);
        //            startPosition = webpageSource.IndexOf(">", startPosition);
        //            endPosition = webpageSource.IndexOf("</span>", startPosition);
        //            symbolData[j, i] = webpageSource.Substring(startPosition+1, endPosition - startPosition - 1);
        //            string some = webpageSource.Substring(datePosition+60, 60);
        //        }
        //        datePosition = webpageSource.IndexOf(CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(DateTime.Now.Month), endPosition);
        //    }
        //    return symbolData;
        //}
    }
}
