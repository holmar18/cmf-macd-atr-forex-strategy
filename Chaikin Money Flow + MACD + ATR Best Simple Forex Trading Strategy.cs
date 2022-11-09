using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class ChaikinMoneyFlowMACDATRBestSimpleForexTradingStrategy : Robot
    {
        [Parameter("Account size" , DefaultValue = 5000, Group = "Account", MaxValue = 1000000, MinValue = 100, Step = 1)]
        public int AccountSize { get; set; }
        
        [Parameter("Risk in %" , DefaultValue = 1.0, Group = "Account", MaxValue = 5.0, MinValue = 0.1, Step = 0.1)]
        public double RiskPercent { get; set; }
        
        [Parameter("SL * Atr" , DefaultValue = 2.0, Group = "Account", MaxValue = 20.0, MinValue = 0.1, Step = 0.1)]
        public double SL_Atr { get; set; }
    
        [Parameter("TP * Atr" , DefaultValue = 4.0, Group = "Account", MaxValue = 40.0, MinValue = 0.1, Step = 0.1)]
        public double TP_Atr { get; set; }
        
        [Parameter("Use SL order" , DefaultValue = true, Group = "Order Settings")]
        public bool Use_SL_Order { get; set; }
        
        [Parameter("SL order Limit range" , DefaultValue = 1, Group = "Order Settings", MaxValue = 40, MinValue = 1, Step = 1)]
        public int SL_Limit_Range { get; set; }
        
        [Parameter("Use trailing Stop", DefaultValue = false, Group = "Order Settings")]
        public bool Use_Trailing_Stop { get; set; }
    
        [Parameter("Periods", DefaultValue = 14, Group = "ATR settings", MinValue = 1, Step = 1)]
        public int ATR_Periods { get; set; }
        
        [Parameter("Type", DefaultValue = MovingAverageType.Exponential, Group = "ATR settings")]
        public MovingAverageType ATR_Type { get; set; }
        
        [Parameter("Long cycle", DefaultValue = 26, Group = "MACD settings", MinValue = 1, Step = 1)]
        public int MACD_long_Cycle { get; set; }

        [Parameter("Short cycle", DefaultValue = 12, Group = "MACD settings", MinValue = 1, Step = 1)]
        public int MACD_short_Cycle { get; set; }

        [Parameter("Signal periods", DefaultValue = 9, Group = "MACD settings", MinValue = 1, Step = 1)]
        public int MACD_signal_Periods { get; set; }
        
        [Parameter("Source", Group = "MACD settings")]
        public DataSeries MACD_signal_Source { get; set; }
        
        [Parameter("Periods", DefaultValue = 20, Group = "Chaikin settings", MinValue = 1, Step = 1)]
        public int CHAIKIN_Periods { get; set; }
        
        [Output("ATR")]
        public IndicatorDataSeries ATR_Draw { get; set; }
        
        [Output("MACD")]
        public IndicatorDataSeries MACD_Draw { get; set; }
        
        [Output("MACD Signal")]
        public IndicatorDataSeries MACD_DrawTwo { get; set; }
        
        [Output("MACD Histogram")]
        public IndicatorDataSeries MACD_DrawThree { get; set; }
        
        [Output("Chaikin Money Flow")]
        public IndicatorDataSeries CHAIKIN_Draw { get; set; }
        
        
        public AverageTrueRange ATR;
        public MacdCrossOver MACD;
        public double MACD_Signal;  // Red line
        public double MACD_Cycle; // Blue line 
        public bool MACD_OverUnder; // Marks the start of macd line Over = true, under = false;
        public ChaikinMoneyFlow CHAIKIN;
        private string CanTrade;
        
        // For macd line crossover
        private int MACD_Obove_Counter = 0;
        private int MACD_Under_Counter = 0;
        private int Last_Crossover_Index = 0;
        private bool LasTraded;
        
        // Trade
        private double LotSize;
        private readonly string PositionId = "PID";
        private double VolumeUnits;
        
        


        protected override void OnStart()
        {
            Set_Atr();
            Set_Chaikin();
            Set_Macd();
            
            // Check start of MACD Lines
            Set_Macd_At_start();
        }


        protected override void OnTick()
        {
            Set_Atr();
            Set_Chaikin();
            Set_Macd();
            
            
            MACD_Crossover_Counter();
            Check_Macd_Crossover();
            Check_Chaikin();
        }


        protected override void OnStop()
        {
            // Handle cBot stop here
        }
        
        
        public void Set_Atr()
        {
            ATR = Indicators.AverageTrueRange(periods: ATR_Periods, maType: ATR_Type);
        }
        
        
        public void Set_Macd()
        {
            MACD = Indicators.MacdCrossOver(source: MACD_signal_Source ,longCycle: MACD_long_Cycle, shortCycle: MACD_short_Cycle, signalPeriods: MACD_signal_Periods);
            MACD_Signal = MACD.Signal[Bars.Count - 1];
            MACD_Cycle = MACD.MACD[Bars.Count - 1];
        }


        public void Set_Chaikin()
        {
            CHAIKIN = Indicators.ChaikinMoneyFlow(periods: CHAIKIN_Periods);
        }
        
        
        private void Color_Candle(int index, Color col)
        {
            Chart.SetBarFillColor(index, col);
            Chart.SetBarOutlineColor(index, col);
        }
        
        public void Check_Macd_Crossover()
        {
            
            int index = Bars.Count - 1;
            
            if(MACD_Obove_Counter == 2 && CanTrade == "LONG" && Positions.Count == 0 && Last_Crossover_Index == Bars.Count - 1)
            {
                // Cycle line is above the signal line
                //MACD_OverUnder = true;
                // Macd has crossed Two times and Chaikin money flow is larger than 0
               Execute_Trade(TradeType.Buy, Bars.HighPrices[index] + 0.0001);
                
            }
            else if(MACD_Under_Counter == 2 && CanTrade == "SHORT" && Positions.Count == 0 && Last_Crossover_Index == Bars.Count - 1)
            {
                Execute_Trade(TradeType.Sell, Bars.HighPrices[index] - 0.0001);
            }
        }
        
        public void MACD_Crossover_Counter()
        {
            // Counts the crossovers under and above the zeroline on the histogram
            double R_MACD_Cycle = Math.Round(MACD_Cycle, 5);
            double R_MACD_Sigla = Math.Round(MACD_Signal, 5);
                
            if(R_MACD_Cycle > 0 && R_MACD_Sigla > 0 && R_MACD_Sigla == R_MACD_Cycle && Last_Crossover_Index != Bars.Count - 1)
            {
                Last_Crossover_Index = Bars.Count - 1;
                MACD_Obove_Counter += 1;
                MACD_Under_Counter = 0;

            } 
            else if (R_MACD_Cycle < 0 && R_MACD_Sigla < 0 && R_MACD_Sigla == R_MACD_Cycle && Last_Crossover_Index != Bars.Count - 1)
            {
                Last_Crossover_Index = Bars.Count - 1;
                MACD_Under_Counter += 1;
                MACD_Obove_Counter = 0;
            }
        }
        
        
        public void Set_Macd_At_start()
        {
            if(MACD_Cycle > MACD_Signal)
            {
                // Cycle line is above the signal line
                MACD_OverUnder = true;
            }
            else 
            {
                MACD_OverUnder = false;
            } 
        }
        
        public void Check_Chaikin()
        {
            if(CHAIKIN.Result.LastValue > 0)
            {
                // Can go long
                CanTrade = "LONG";
            } 
            else if(CHAIKIN.Result.LastValue == 0)
            {
                CanTrade = "NO-TRADE";
            }
            else
            {
                CanTrade = "SHORT";
            }
            
        }
        
        
        public void Execute_Trade(TradeType tType, double highLow)
        {
            CalculateLotSize();
            
            double stopLoss = Math.Round(ATR.Result.LastValue * SL_Atr, 5) * 10000;
            double takeProfit = Math.Round(ATR.Result.LastValue * TP_Atr, 5) * 10000;
            VolumeUnits = Symbol.QuantityToVolumeInUnits(LotSize);
            
            Random rnd = new Random();
            int num = rnd.Next();
            string id = String.Format("{0}", num);
            string index = string.Format("{0}", Bars.Count - 1);
            int bar_Index = Bars.Count - 1;
            
            Chart.DrawText(id, index, bar_Index, highLow, Color.AliceBlue);
            Color_Candle(bar_Index, Color.Pink);
            
            if(Use_SL_Order)
            {   
                double tPrice = tType == TradeType.Sell ? Bars.OpenPrices[Bars.Count - 1] : Bars.ClosePrices[Bars.Count - 1];

                DateTime time = new DateTime();
                DateTime expire = time.AddMinutes(4);
                PlaceStopLimitOrder(tType, SymbolName, VolumeUnits, tPrice, SL_Limit_Range, "SL-ORDER", stopLoss, takeProfit, expire);
            }
            else
            {
                ExecuteMarketOrder(tType, SymbolName, VolumeUnits, PositionId, stopLoss, takeProfit, "Trailing", Use_Trailing_Stop);
            }
            
            //ExecuteMarketOrder(tType, SymbolName, VolumeUnits, PositionId, stopLoss, takeProfit, "Trailing", true);
            
            Random rndSL = new Random();
            int numSL = rndSL.Next();
            string idSl = String.Format("{0}", numSL);
            Random rndTP = new Random();
            int numTP = rndTP.Next();
            string idTP = String.Format("{0}", numTP);


            if(tType == TradeType.Sell)
            {
                Chart.DrawText(idSl, "SL", bar_Index, highLow + (ATR.Result.LastValue * 2) , Color.Red);
            
                Chart.DrawText(idTP, "TP", bar_Index, highLow - (ATR.Result.LastValue * 4) , Color.Green);
            }
            else 
            {
                Chart.DrawText(idSl, "SL", bar_Index, highLow - (ATR.Result.LastValue * 2) , Color.Red);
            
                Chart.DrawText(idTP, "TP", bar_Index, highLow + (ATR.Result.LastValue * 4) , Color.Green);
            }

            
        }
        
        
        private void CalculateLotSize()
        {
            double accSizeInt = Convert.ToDouble(AccountSize);
            double riskPercentInt = Convert.ToDouble(RiskPercent);
            double lotSize = ((accSizeInt * (riskPercentInt / 100)) / ATR.Result.LastValue * 2) / 100000;
            double lotSizeRounded = Math.Round(lotSize, 2);
            
            LotSize = lotSizeRounded;
        }
    }
}
