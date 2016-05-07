using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace BraneCloud.Evolution.EC.App.Stock
{
    public class Trader
    {
        // Stock sector, value 1-12
        public static int sector = 4;

        // boolean used to determine whether date range should be applied
        public static bool applyDateRange = true;
        public static string startTrainDate = "1/1/2010";
        public static string endTrainDate = "12/31/2010";
        public static string startTestDate = "7/1/2011";
        public static string endTestDate = "8/1/2014";
        public string mode = "Train";

        // Sequentially increasing counter used to identify strategy
        public static int strategyCounter = 0;
        public static bool buyLong = true;
        public static bool sellShort = true;
        public static bool usePositionScore = true;

        // used to initiate AmiBroker
        static dynamic runner = Activator.CreateInstance(Type.GetTypeFromProgID("Broker.Application", true));
        static dynamic analysis;

        // parent path
        //string baseDirectoryPath = "c:\\Program Files\\Amibroker\\Formulas\\Custom\\";
        string baseDirectoryPath = "d:\\Amibroker\\";
        //string reportDirectoryPath = "c:\\Program Files\\Amibroker\\Reports\\";

        // Results of strategy based on parsed backtest results
        public Dictionary<string, double> strategyResults = new Dictionary<string, double>();

        public Trader()
        {
            // Clean up .apx and csv files
            string directoryPath = baseDirectoryPath + "temp\\";
            Directory.GetFiles(directoryPath).ToList().ForEach(File.Delete);

            Console.WriteLine("Cleaning up reports..");
            //Directory.GetDirectories(reportDirectoryPath).ToList().ForEach(Directory.Delete);
            //Directory.GetFiles(reportDirectoryPath).ToList().ForEach(File.Delete);
            Console.WriteLine("Done!");
        }

        // Generate AFL file accounting for timing issues and xml file updates 
        private void generateAFLFile(StringBuilder AFLText, string fileName, string start, string end)
        {
            // delete AFL file to prevent synch errors with APX file
            File.Delete(baseDirectoryPath + fileName);

            // update APX file
            string apxFileName = baseDirectoryPath + "temp\\genetic" + strategyCounter + ".apx";
            File.Copy(baseDirectoryPath + "genetic.apx", apxFileName);
            XmlTextReader reader = new XmlTextReader(apxFileName);
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);
            reader.Close();

            XmlNode oldNode;
            XmlElement root = doc.DocumentElement;

            //Select the formula node
            oldNode = root.SelectSingleNode("/AmiBroker-Analysis/General/FormulaContent");
            oldNode.InnerText = AFLText.ToString();

            //Select the sector node 
            oldNode = root.SelectSingleNode("/AmiBroker-Analysis/General/IncludeFilter/SectorID");
            oldNode.InnerText = sector.ToString();

            if (applyDateRange)
            {
                //Select the from date node
                oldNode = root.SelectSingleNode("/AmiBroker-Analysis/General/FromDate");
                oldNode.InnerText = start;

                //Select the to date node 
                oldNode = root.SelectSingleNode("/AmiBroker-Analysis/General/ToDate");
                oldNode.InnerText = end;
            }


            string tradeType = "";
            if (buyLong && sellShort)
            {
                tradeType = "3";
            }
            else if (sellShort)
            {
                tradeType = "2";
            }
            else if (buyLong)
            {
                tradeType = "1";
            }
            //Select the Trade Type node 
            oldNode = root.SelectSingleNode("/AmiBroker-Analysis/BacktestSettings/TradeFlags");
            oldNode.InnerText = tradeType;

            //save the output to a file
            doc.Save(apxFileName);
        }

        // Run AFL file, accounting for AmiBroker API idiosyncrasies
        private void runAFLFile()
        {
            if (strategyCounter % 2000 == 0)
            {
                foreach (var process in Process.GetProcesses())
                {
                    if (process.ProcessName == "Broker")
                    {
                        // kill the process to get around a problem with the process hanging after 4821 analyses
                        process.Kill();
                        // wait for the process to die, which resolves a COM Exception
                        System.Threading.Thread.Sleep(5000);
                        // recreate the analysis runner instance
                        runner = Activator.CreateInstance(Type.GetTypeFromProgID("Broker.Application", true));
                    }
                }
            }


            // perform analysis
            analysis = runner.AnalysisDocs.Open(baseDirectoryPath + "temp\\genetic" + strategyCounter + ".apx");
            analysis.Run(2);
            while (analysis.IsBusy)
            {
                System.Threading.Thread.Sleep(20);
            }

            // export result list to CSV file
            analysis.Export(baseDirectoryPath + "temp\\genetic" + strategyCounter + ".csv");
            while (analysis.IsBusy) System.Threading.Thread.Sleep(500);
            analysis.Close();
        }

        // Parse csv report file produced by AmiBroker
        private void populateAFLResults(string fileName)
        {
            string[] names;
            string[] values;
            try
            {
                // Wait for .csv file to be created
                while (!File.Exists(baseDirectoryPath + "temp\\" + fileName))
                {
                    Console.WriteLine("Need to wait a bit...");
                }

                StreamReader resultsFile = new StreamReader(baseDirectoryPath + "temp\\" + fileName);
                names = resultsFile.ReadLine().Split(',');
                values = resultsFile.ReadLine().Split(',');
                resultsFile.Close();
                int range = Math.Min(names.Length, values.Length);
                for (int i = 0; i < range; i++)
                {
                    if (!names[i].StartsWith("Ticker"))
                    {
                        if (values[i].Contains("N/A"))
                        {
                            strategyResults[names[i]] = -999.0;
                        }
                        else
                        {
                            double val = double.Parse(values[i]);
                            strategyResults[names[i]] = val;
                        }
                    }
                }
            }
            catch (FileNotFoundException fnf)
            {
                Console.WriteLine("Results csv not found: " + baseDirectoryPath + fileName);
                throw fnf;
            }

            File.Delete(baseDirectoryPath + "genetic.csv");
            File.Delete(baseDirectoryPath + "genetic.afl");

        }


        public double StrategyFitness(StringBuilder strategyText)
        {
            // Strategy fitness
            double fitness = 0.0d;

            // Strategy results data
            string resultsData = "";

            // Step 1 - Generate AFL File
            if (this.mode == "Test")
            {
                generateAFLFile(strategyText, "genetic.afl", startTestDate, endTestDate);
            }
            else
            {
                generateAFLFile(strategyText, "genetic.afl", startTrainDate, endTrainDate);
            }

            // Step 2 - Run AFL File
            runAFLFile();

            // Step 3 - Retrieve results of backtesting
            populateAFLResults("genetic" + strategyCounter + ".csv");

            // Step 4 - Calculate fitness value
            double numWinners = strategyResults["# of winners"];
            double sharpe = strategyResults["Sharpe Ratio"];
            double numLosers = strategyResults["# of losers"];
            double avgBarsHeld = strategyResults["Avg Bars Held"];
            double netProfit = strategyResults["Net Profit"];

            if (numWinners == 0.0d)
            {
                fitness = 0.0d;
            }
            else if (netProfit < 0.0d)
            {
                fitness = 1000000.0d + netProfit;
            }

            else
            {
                // fitness mode A
                //fitness = 1000000.0d - netProfit;

                // fitness mode B
                //fitness = (numWinners / (numLosers + 1.0d));

                // fitness mode C
                //fitness = (numWinners / (numLosers + numWinners)) * strategyResults["Net Profit"] / avgBarsHeld;

                // fitness mode D
                //fitness = numWinners;

                // fitness mode E
                //fitness = (numWinners / (numLosers + numWinners)) * (200000.0d + netProfit) / Math.Log10(avgBarsHeld);

                // fitness mode F
                fitness = 2000000.0d + sharpe;
            }


            resultsData = strategyCounter + "\t" + "sec:" + sector + "\t" + DateTime.Now + "\t" + "fit:" + fitness.ToString() + "\t" + mode +
                "\t" + "winners:" + strategyResults["# of winners"] + "\t" + "losers:" + strategyResults["# of losers"] + "\t" + "barsHeld:" + avgBarsHeld +
                "\t" + "netProfit:" + strategyResults["Net Profit"] + "\t" + "avgProfitPct" + strategyResults["Avg % Profit/Loss"] +
                "\t" + "trainDates:" + startTrainDate + "\t" + endTrainDate + "\t" + "testDates:" + startTestDate + "\t" + endTestDate +
                "\t" + "long?" + buyLong + "\t" + "short?:" + sellShort;

            Console.WriteLine(resultsData);

            strategyCounter++;
            return fitness;

        }
    }
}
