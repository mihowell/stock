/*
 * Stock analysis 
 */

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using BraneCloud.Evolution.EC.Configuration;
using BraneCloud.Evolution.EC.GP;
using BraneCloud.Evolution.EC.GP.Koza;
using BraneCloud.Evolution.EC.Simple;
using BraneCloud.Evolution.EC.Support;
using BraneCloud.Evolution.EC.App.Stock;

namespace BraneCloud.Evolution.EC.App.StockApp
{
    /// <summary>
    /// Stock generates AmiBroker (www.amibroker.com) AFL scripts which are then used
    /// to backtest stock trades. A fitness is generated for each script based on trading results
    /// </summary>
 
    [ECConfiguration("ec.app.stock.Stock")]
    public class Stock : GPProblem, ISimpleProblem
    {
        #region Constants
        
        #endregion // Constants

        #region Properties
        public Dictionary<string, string> translate = new Dictionary<string,string>();
        Trader makemomoney;

        #endregion // Properties

        #region Cloning

        public override object Clone()
        {
            var myobj = (Stock)(base.Clone());
            return myobj;
        }

        #endregion // Cloning
        #region Setup

        public override void Setup(IEvolutionState state, IParameter paramBase)
        {
            // very important, remember this
            base.Setup(state, paramBase);

            // not using any default base -- it's not safe

        }

        #endregion // Setup
        #region Problem


        public Stock()
        {
            makemomoney = new Trader();

            translate["traderule"] = "";
            translate["buy"] = "buy = ";
            translate["sell"] = "sell = ";
            translate["short"] = "short = ";
            translate["cover"] = "cover = ";
            translate["and"] = " and ";
            translate["or"] = " or ";
            translate["lt"] = " < ";
            translate["gt"] = " > ";
            translate["high"] = " h ";
            translate["low"] = " l ";
            translate["close"] = " c ";
            translate["stema"] = " ema(c,13) ";
            translate["mtema"] = " ema(c,39) ";
            translate["ltema"] = " ema(c,65) ";
            translate["atrlow"] = " (atr(14) < (c * 0.02)) ";
            translate["atrmid"] = " (atr(14) >= (c * 0.02)) AND (atr(14) <= (c * 0.05)) ";
            translate["atrhigh"] = " (atr(14) > (c * 0.05)) ";
            translate["adxlow"] = " (adx(14) < 20) ";
            translate["adxmid"] = " (adx(14) >= 20) AND (adx(14) <= 25) ";
            translate["adxhigh"] = " (adx(14) > 25) ";
            translate["pdimdilow"] = " (pdi(14)/mdi(14) < 0.9) ";
            translate["pdimdimid"] = " (pdi(14)/mdi(14) > 0.9) AND (pdi(14)/mdi(14) < 1.1) ";
            translate["pdimdihigh"] = " (pdi(14)/mdi(14) > 1.1) ";
            translate["ccilow"] = " (cross(-100,CCI())) ";
            translate["ccihigh"] = " (cross(CCI(),100)) ";
            translate["tsf"] = " TSF(C,13) ";
            translate["cross"] = "cross";
            translate["gapup"] = " GapUp() ";
            translate["gapdown"] = " GapDown() ";
            translate["inside"] = " Inside() ";
            translate["outside"] = " Outside() ";
            translate["accdistup"] = " (cross(EMA(accdist(),13),EMA(accdist(),39))) ";
            translate["accdistdown"] = " (cross(EMA(accdist(),39),EMA(accdist(),13))) ";
            translate["macdup"] = " (cross(macd(13,39),signal(13,39,10))) ";
            translate["macddown"] = " (cross(signal(13,39,10),macd(13,39))) ";
            translate["obvtrend"] = " (MA(OBV(),20) - Ref( MA(OBV(),20),-20)) / Ref(MA(OBV(),20),-20) ";
            translate["obvdownalittle"] = " -0.05 ";
            translate["obvdownalot"] = " -0.12 ";
            translate["obvupalittle"] = " 0.05 ";
            translate["obvupalot"] = " 0.12 ";

        }
        private StringBuilder GPTreeToAFLString(GP.GPNode node)
        {
            StringBuilder sb = new StringBuilder();

            switch (node.Name)
            {
                case "traderule":
                    {
                        foreach (var child in node.Children)
                        {
                            sb.Append(GPTreeToAFLString(child));
                        }
                        return sb;
                    }
                case "obvtrendnode":
                    {
                        foreach (var child in node.Children)
                        {
                            sb.Append(GPTreeToAFLString(child));
                        }
                        return sb;
                    }

                case "buy":
                case "sell":
                case "short":
                case "cover":
                    {
                        sb.Append(translate[node.Name]);
                        foreach (var child in node.Children)
                        {
                            sb.Append(GPTreeToAFLString(child));
                        }
                        sb.Append(";\n");
                        return sb;
                    }
                case "cross":
                    {
                        sb.Append(translate[node.Name]);
                        sb.Append("(");
                        sb.Append(GPTreeToAFLString(node.Children[0]));
                        sb.Append(",");
                        sb.Append(GPTreeToAFLString(node.Children[1]));
                        sb.Append(")");
                        return sb;

                    }
                case "and":
                case "or":
                case "lt":
                case "gt":
                    {
                        sb.Append("(");
                        sb.Append(GPTreeToAFLString(node.Children[0]));
                        sb.Append(translate[node.Name]);
                        sb.Append(GPTreeToAFLString(node.Children[1]));
                        sb.Append(")");
                        return sb;
                    }
                case "high":
                case "low":
                case "close":
                case "stema":
                case "mtema":
                case "ltema":
                case "atrlow":
                case "atrmid":
                case "atrhigh":
                case "adxlow":
                case "adxmid":
                case "adxhigh":
                case "pdimdilow":
                case "pdimdimid":
                case "pdimdihigh":
                case "ccilow":
                case "ccihigh":
                case "tsf":
                case "gapup":
                case "gapdown":
                case "inside":
                case "outside":
                case "accdistup":
                case "accdistdown":
                case "macdup":
                case "macddown":
                case "obvtrend":
                case "obvdownalittle":
                case "obvdownalot": 
                case "obvupalittle":
                case "obvupalot":
                    {
                        sb.Append(translate[node.Name]);
                        return sb;
                    }
                default:
                    {
                        sb.Append("ERROR with" + node.Name);
                        return sb;
                    }
            }

        }


        public void Evaluate(IEvolutionState state, Individual ind, int subpop, int threadnum)
        {
            if (!ind.Evaluated)  // don't bother reevaluating
            {

                ((GP.GPIndividual)ind).PrintIndividualForHumans(state,0);
                var tree = ((GP.GPIndividual)ind).Trees[0];

                StringBuilder tradingrule = new StringBuilder();
                tradingrule.Append("PositionSize = 1000;\n");
                tradingrule.Append("SetOption(\"SeparateLongShortRank\", True );\n");
                tradingrule.Append("SetOption(\"MaxOpenLong\", 5 );\n");
                tradingrule.Append("SetOption(\"MaxOpenShort\", 5 );\n");
                tradingrule.Append("SetOption(\"SeparateLongShortRank\", True );\n");
                tradingrule.Append("SetOption(\"WorstRankHeld\", 5);\n");

                tradingrule.Append(GPTreeToAFLString(tree.Child));
                double fitval = makemomoney.StrategyFitness(tradingrule);

                // the fitness better be KozaFitness!
                var f = ((KozaFitness)ind.Fitness);
                f.SetStandardizedFitness(state, (float)fitval);
                ind.Evaluated = true;

            }
        }

        public override void Describe(IEvolutionState state, Individual ind, int subpopulation, int threadnum, int log)
        {
            state.Output.PrintLn("\n\nBest Individual\n=====================", log);
            ((GP.GPIndividual)ind).PrintIndividualForHumans(state, 0);
            state.Output.PrintLn("=====================",log);
        }

        #endregion // Problem
    }
}