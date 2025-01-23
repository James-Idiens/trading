#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class WilliamsRenko : Strategy
    {
        private int williamsRPeriod = 14;
        private int trailStopDistance = 80;
        private int profitTargetTicks = 80;
        private double overboughtLevel = -10;
        private double oversoldLevel = -90;

        private double dailyGoal = 1000;
        private double dailyLossLimit = -1000;
        private DateTime lastTradeDate = DateTime.MinValue;
        private double entryPrice = 0;
        private TimeSpan tradingStartTime = new TimeSpan(3, 30, 0); // 3:30 AM NZT
        private TimeSpan tradingEndTime = new TimeSpan(4, 0, 0); // 4:00 AM NZT

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Renko strategy using Williams %R with momentum-based entries and Renko candle exits.";
                Name = "WilliamsRenko";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                BarsRequiredToTrade = 20;
                StartBehavior = StartBehavior.WaitUntilFlat;
                Slippage = 0;
                TimeInForce = Cbi.TimeInForce.Gtc;
                TraceOrders = false;
                IsInstantiatedOnEachOptimizationIteration = false;
            }
            else if (State == State.DataLoaded)
            {
                lastTradeDate = DateTime.MinValue;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            // Reset daily PNL at the start of a new trading day
            if (Time[0].Date != lastTradeDate.Date)
            {
                lastTradeDate = Time[0].Date;
            }

            // Ensure we are within trading hours
            TimeSpan currentTime = Time[0].TimeOfDay;
            if (currentTime < tradingStartTime || currentTime > tradingEndTime)
                return;

            // Check if cumulative profit/loss has reached daily limits
            double cumulativeProfitLoss = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;

            if (cumulativeProfitLoss >= dailyGoal || cumulativeProfitLoss <= dailyLossLimit)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    ExitLong();
                return;
            }

            // Calculate Williams %R
            double williamsRValue = WilliamsR(williamsRPeriod)[0];

            // Entry Logic
            if (williamsRValue > overboughtLevel && Position.MarketPosition != MarketPosition.Long)
            {
                EnterLong("LongRenko");
                entryPrice = Close[0]; // Set the entry price when entering the trade
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);
                SetProfitTarget(CalculationMode.Ticks, profitTargetTicks); // Add profit target here
            }
            else if (williamsRValue < oversoldLevel && Position.MarketPosition != MarketPosition.Short)
            {
                EnterShort("ShortRenko");
                entryPrice = Close[0]; // Set the entry price when entering the trade
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);
                SetProfitTarget(CalculationMode.Ticks, profitTargetTicks); // Add profit target here
            }
        }

        protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Real-time PNL calculation is now handled by the SystemPerformance object,
            // so we don't need to manually update daily PNL here.
        }

        #region Properties
        [Display(Name = "Start Time", Description = "Trading session start time", Order = 1, GroupName = "Parameters")]
        public TimeSpan TradingStartTime
        {
            get { return tradingStartTime; }
            set { tradingStartTime = value; }
        }

        [Display(Name = "End Time", Description = "Trading session end time", Order = 2, GroupName = "Parameters")]
        public TimeSpan TradingEndTime
        {
            get { return tradingEndTime; }
            set { tradingEndTime = value; }
        }

        [Display(Name = "Daily Profit Goal", Description = "Maximum profit allowed per day", Order = 3, GroupName = "Parameters")]
        public double DailyGoal
        {
            get { return dailyGoal; }
            set { dailyGoal = value; }
        }

        [Display(Name = "Daily Loss Limit", Description = "Maximum loss allowed per day", Order = 4, GroupName = "Parameters")]
        public double DailyLossLimit
        {
            get { return dailyLossLimit; }
            set { dailyLossLimit = value; }
        }

        [Display(Name = "Trail Stop Distance (Ticks)", Description = "Trailing stop distance in ticks", Order = 5, GroupName = "Parameters")]
        public int TrailStopDistance
        {
            get { return trailStopDistance; }
            set { trailStopDistance = value; }
        }

        [Display(Name = "Profit Target (Ticks)", Description = "Profit target in ticks", Order = 6, GroupName = "Parameters")]
        public int ProfitTargetTicks
        {
            get { return profitTargetTicks; }
            set { profitTargetTicks = value; }
        }
        #endregion
    }
}
