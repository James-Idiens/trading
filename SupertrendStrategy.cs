using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.Data;
using System.Windows.Media;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using System.ComponentModel.DataAnnotations;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class SupertrendRenkoStrategy : Strategy
    {
        private double superTrendValue;
        private double prevSuperTrendValue;
        private bool isLong;
        private Series<double> superTrendSeries;
        private TimeSpan tradingStartTime = new TimeSpan(9, 30, 0); // Default start time 9:30 AM
        private TimeSpan tradingEndTime = new TimeSpan(16, 0, 0);   // Default end time 4:00 PM

        [NinjaScriptProperty]
        public int ATRPeriod { get; set; } = 10;

        [NinjaScriptProperty]
        public double Multiplier { get; set; } = 1.5;

        [NinjaScriptProperty]
        public int Contracts { get; set; } = 1;

        [NinjaScriptProperty]
        public int TakeProfitTicks { get; set; } = 10;

        [Display(Name = "Start Time", Description = "Trading session start time", Order = 5, GroupName = "Trading Hours")]
        public TimeSpan TradingStartTime
        {
            get { return tradingStartTime; }
            set { tradingStartTime = value; }
        }

        [Display(Name = "End Time", Description = "Trading session end time", Order = 6, GroupName = "Trading Hours")]
        public TimeSpan TradingEndTime
        {
            get { return tradingEndTime; }
            set { tradingEndTime = value; }
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Supertrend Renko Strategy";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                DefaultQuantity = 1;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsOverlay = true;
                AddPlot(Brushes.Green, "Supertrend");
            }
            else if (State == State.DataLoaded)
            {
                superTrendSeries = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < ATRPeriod) return;

            // Check if we're within trading hours before proceeding
            TimeSpan currentTime = Time[0].TimeOfDay;

            if (tradingStartTime < tradingEndTime)
            {
                // Standard case: Start time is earlier than end time (e.g., 9:30 to 16:00)
                if (currentTime < tradingStartTime || currentTime > tradingEndTime)
                    return;
            }
            else
            {
                // Crossover case: Start time is later than end time (e.g., 23:00 to 04:00)
                if (currentTime < tradingStartTime && currentTime > tradingEndTime)
                    return;
            }

            // Calculate ATR
            double atr = ATR(ATRPeriod)[0];

            // Calculate Supertrend
            double basicUpperBand = (High[0] + Low[0]) / 2 + (Multiplier * atr);
            double basicLowerBand = (High[0] + Low[0]) / 2 - (Multiplier * atr);

            if (Close[1] > prevSuperTrendValue)
                superTrendValue = Math.Max(basicLowerBand, prevSuperTrendValue);
            else if (Close[1] < prevSuperTrendValue)
                superTrendValue = Math.Min(basicUpperBand, prevSuperTrendValue);

            prevSuperTrendValue = superTrendValue;
            superTrendSeries[0] = superTrendValue;
            Values[0][0] = superTrendValue;

            // Change plot color based on trend direction
            PlotBrushes[0][0] = (Close[0] > superTrendValue) ? Brushes.Green : Brushes.Red;

            // Entry Conditions - Only act on bar close and within trading hours
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (Close[1] > superTrendSeries[1] && Close[0] > superTrendValue)
                {
                    EnterLong(Contracts, "SupertrendLong");
                    isLong = true;

                    double longTakeProfit = Close[0] + TakeProfitTicks * TickSize;
                    SetProfitTarget("SupertrendLong", CalculationMode.Price, longTakeProfit);
                }
                else if (Close[1] < superTrendSeries[1] && Close[0] < superTrendValue)
                {
                    EnterShort(Contracts, "SupertrendShort");
                    isLong = false;

                    double shortTakeProfit = Close[0] - TakeProfitTicks * TickSize;
                    SetProfitTarget("SupertrendShort", CalculationMode.Price, shortTakeProfit);
                }
            }
            else
            {
                if ((isLong && Close[1] > superTrendSeries[1] && Close[0] < superTrendValue) || 
                    (!isLong && Close[1] < superTrendSeries[1] && Close[0] > superTrendValue))
                {
                    ExitLong("ExitLong", "SupertrendLong");
                    ExitShort("ExitShort", "SupertrendShort");
                }
            }
        }
    }
}
