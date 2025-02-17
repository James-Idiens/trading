#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
using NinjaTrader.NinjaScript.Indicators;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class WilliamsRenko : Strategy
    {
        private WilliamsR WilliamsR1;
        private int williamsRPeriod = 20;
        private int trailStopDistance = 65;
        private int profitTargetTicks = 80;
        private double overboughtLevel = -20;
        private double oversoldLevel = -80;
        private int wmaPeriod = 100;
        private bool useWmaFilter = false;
        private int orderQuantity = 1;
        private double dailyGoal = 1000;
        private double dailyLossLimit = -1000;
        private DateTime lastTradeDate = DateTime.MinValue;
        private double entryPrice = 0;
        private TimeSpan tradingStartTime = new TimeSpan(3, 30, 0);
        private TimeSpan tradingEndTime = new TimeSpan(4, 0, 0);

        // New variables for cooldown
        private bool profitTargetHit = false;
        private int barsSinceProfitTarget = 0;
        private int cooldownPeriod = 0;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Renko strategy using Williams %R with momentum-based entries and WMA filter.";
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
            else if (State == State.Configure)
            {
                // The Secondary data series for 2000 tick chart
                AddDataSeries(Data.BarsPeriodType.Tick, 2000);
            }
            else if (State == State.DataLoaded)
            {
                WilliamsR1 = WilliamsR(Closes[1], williamsRPeriod);
                lastTradeDate = DateTime.MinValue;
                profitTargetHit = false;
                barsSinceProfitTarget = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return; // Only process on primary series

            if (CurrentBar < BarsRequiredToTrade || CurrentBars[1] < williamsRPeriod)
                return;

            if (Time[0].Date != lastTradeDate.Date)
            {
                lastTradeDate = Time[0].Date;
                profitTargetHit = false;
                barsSinceProfitTarget = 0;
            }

            if (profitTargetHit)
            {
                barsSinceProfitTarget++;
            }

            if (profitTargetHit && barsSinceProfitTarget >= cooldownPeriod)
            {
                profitTargetHit = false;
                barsSinceProfitTarget = 0;
            }

            TimeSpan currentTime = Time[0].TimeOfDay;
            double cumulativeProfitLoss = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;

            if (tradingStartTime < tradingEndTime)
            {
                if (currentTime < tradingStartTime || currentTime > tradingEndTime)
                    return;
            }
            else
            {
                if (currentTime < tradingStartTime && currentTime > tradingEndTime)
                    return;
            }

            // The Williams %R gets calculated on the secondary data series (2000 tick chart)
            double williamsRValue = WilliamsR1[0];

            // Calculate WMA on primary series
            double wmaValue = WMA(wmaPeriod)[0];

            bool wmaConditionLong = !useWmaFilter || (useWmaFilter && Close[0] > wmaValue);
            bool wmaConditionShort = !useWmaFilter || (useWmaFilter && Close[0] < wmaValue);


            // Determine if the current Renko bar is green (bullish) or red (bearish)
            bool renkoUp = Close[0] > Open[0];
            bool renkoDown = Close[0] < Open[0];

            if (williamsRValue > overboughtLevel &&
              Position.MarketPosition != MarketPosition.Long &&
              !profitTargetHit &&
              wmaConditionLong &&
              renkoUp)
            {
                EnterLong(orderQuantity, "LongRenko");
                entryPrice = Close[0];
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);
                SetProfitTarget(CalculationMode.Ticks, profitTargetTicks);
            }
            else if (williamsRValue < oversoldLevel &&
              Position.MarketPosition != MarketPosition.Short &&
              !profitTargetHit &&
              wmaConditionShort &&
              renkoDown)
            {
                EnterShort(orderQuantity, "ShortRenko");
                entryPrice = Close[0];
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);
                SetProfitTarget(CalculationMode.Ticks, profitTargetTicks);
            }
        }

        #region Properties

        [Display(Name = "Order Quantity", Description = "Number of contracts per trade", Order = 1, GroupName = "Parameters")]
        public int OrderQuantity
        {
            get
            {
                return orderQuantity;
            }
            set
            {
                orderQuantity = Math.Max(1, value);
            }
        }
        [Display(Name = "Start Time", Description = "Trading session start time", Order = 2, GroupName = "Parameters")]
        public TimeSpan TradingStartTime
        {
            get
            {
                return tradingStartTime;
            }
            set
            {
                tradingStartTime = value;
            }
        }

        [Display(Name = "End Time", Description = "Trading session end time", Order = 3, GroupName = "Parameters")]
        public TimeSpan TradingEndTime
        {
            get
            {
                return tradingEndTime;
            }
            set
            {
                tradingEndTime = value;
            }
        }

        [Display(Name = "Daily Profit Goal", Description = "Maximum profit allowed per day", Order = 4, GroupName = "Parameters")]
        public double DailyGoal
        {
            get
            {
                return dailyGoal;
            }
            set
            {
                dailyGoal = value;
            }
        }

        [Display(Name = "Daily Loss Limit", Description = "Maximum loss allowed per day", Order = 5, GroupName = "Parameters")]
        public double DailyLossLimit
        {
            get
            {
                return dailyLossLimit;
            }
            set
            {
                dailyLossLimit = value;
            }
        }

        [Display(Name = "Trail Stop Distance (Ticks)", Description = "Trailing stop distance in ticks", Order = 6, GroupName = "Parameters")]
        public int TrailStopDistance
        {
            get
            {
                return trailStopDistance;
            }
            set
            {
                trailStopDistance = value;
            }
        }

        [Display(Name = "Profit Target (Ticks)", Description = "Profit target in ticks", Order = 7, GroupName = "Parameters")]
        public int ProfitTargetTicks
        {
            get
            {
                return profitTargetTicks;
            }
            set
            {
                profitTargetTicks = value;
            }
        }

        [Display(Name = "Williams %R Period", Description = "Number of periods for Williams %R calculation", Order = 8, GroupName = "Parameters")]
        public int WilliamsRPeriod
        {
            get
            {
                return williamsRPeriod;
            }
            set
            {
                williamsRPeriod = Math.Max(1, value);
            }
        }

        [Display(Name = "Overbought Level", Description = "Williams %R level considered overbought", Order = 9, GroupName = "Parameters")]
        public double OverboughtLevel
        {
            get
            {
                return overboughtLevel;
            }
            set
            {
                overboughtLevel = value;
            }
        }

        [Display(Name = "Oversold Level", Description = "Williams %R level considered oversold", Order = 10, GroupName = "Parameters")]
        public double OversoldLevel
        {
            get
            {
                return oversoldLevel;
            }
            set
            {
                oversoldLevel = value;
            }
        }

        [Display(Name = "Cooldown Bars", Description = "Number of bars to wait after hitting profit target", Order = 11, GroupName = "Parameters")]
        public int CooldownPeriod
        {
            get
            {
                return cooldownPeriod;
            }
            set
            {
                cooldownPeriod = Math.Max(0, value);
            }
        }

        [Display(Name = "WMA Period", Description = "Number of periods for Weighted Moving Average calculation", Order = 12, GroupName = "Parameters")]
        public int WmaPeriod
        {
            get
            {
                return wmaPeriod;
            }
            set
            {
                wmaPeriod = Math.Max(1, value);
            }
        }

        [Display(Name = "Use WMA Filter", Description = "Enable/Disable WMA filter for trade entries", Order = 13, GroupName = "Parameters")]
        public bool UseWmaFilter
        {
            get
            {
                return useWmaFilter;
            }
            set
            {
                useWmaFilter = value;
            }
        }
        #endregion
    }
}
