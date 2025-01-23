#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class TOWilliamsRenko : Strategy
    {
        private int williamsRPeriod = 14;
        private int trailStopDistance = 80;
        private int profitTargetTicks = 80;
        private double overboughtLevel = -20;
        private double oversoldLevel = -80;

        private double dailyPNL;
        private double dailyGoal = 1000;
        private double dailyLossLimit = -1000;
        private DateTime lastTradeDate = DateTime.MinValue;
        private double entryPrice;
        private TimeSpan tradingStartTime = new TimeSpan(3, 3, 0); // 3:30 AM NZT
        private TimeSpan tradingEndTime = new TimeSpan(4, 0, 0); // 4:00 AM NZT

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Renko strategy using Williams %R with momentum-based entries and Renko candle exits.";
                Name = "TOWilliamsRenko";
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
                dailyPNL = 0;
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
                dailyPNL = 0;
            }

            // Ensure we are within trading hours
            TimeSpan currentTime = Time[0].TimeOfDay;
            if (currentTime < tradingStartTime || currentTime > tradingEndTime)
                return;

            // Stop trading if daily profit or loss limits are reached
            if (dailyPNL >= dailyGoal || dailyPNL <= dailyLossLimit)
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
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);

                // Adjust profit target for final trade of the day
                if (dailyPNL + (profitTargetTicks * TickSize) >= dailyGoal)
                {
                    SetProfitTarget(CalculationMode.Currency, dailyGoal - dailyPNL);
                }
                else
                {
                    SetProfitTarget(CalculationMode.Ticks, profitTargetTicks);
                }
            }
            else if (williamsRValue < oversoldLevel && Position.MarketPosition != MarketPosition.Short)
            {
                EnterShort("ShortRenko");
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);

                if (dailyPNL + (profitTargetTicks * TickSize) >= dailyGoal)
                {
                    SetProfitTarget(CalculationMode.Currency, dailyGoal - dailyPNL);
                }
                else
                {
                    SetProfitTarget(CalculationMode.Ticks, profitTargetTicks);
                }
            }
        }

        protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Update daily PNL
            if (execution.Order.OrderState == OrderState.Filled)
            {
                if (execution.Order.OrderAction == OrderAction.Buy || execution.Order.OrderAction == OrderAction.SellShort)
                {
                    entryPrice = price;
                }
                else if (execution.Order.OrderAction == OrderAction.Sell || execution.Order.OrderAction == OrderAction.BuyToCover)
                {
                    double tradeProfit = (price - entryPrice) * quantity * (marketPosition == MarketPosition.Long ? 1 : -1);
                    dailyPNL += tradeProfit;
                }
            }
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
