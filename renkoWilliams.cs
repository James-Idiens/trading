#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using System.ComponentModel.DataAnnotations;
#endregion

namespace NinjaTrader.NinjaScript.Strategies {
  public class TOWilliamsRenko: Strategy {
    private int williamsRPeriod = 14;
    private int trailStopDistance = 80;
    private int profitTargetTicks = 80;
    private double overboughtLevel = -20;
    private double oversoldLevel = -80;

    // New parameters for time filter and P/L tracking
    private TimeSpan startTime = new TimeSpan(7, 0, 0); // 7:00 AM
    private TimeSpan endTime = new TimeSpan(10, 0, 0); // 10:00 AM
    private double maxDailyProfit = 500; // $500 max daily profit
    private double maxDailyLoss = 300; // $300 max daily loss
    private double dailyProfit = 0; // Tracks daily profit
    private DateTime lastTradeDate = DateTime.MinValue;

    protected override void OnStateChange() {
      if (State == State.SetDefaults) {
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
      } else if (State == State.DataLoaded) {
        // Reset daily tracking values
        dailyProfit = 0;
        lastTradeDate = DateTime.MinValue;
      }
    }

    protected override void OnBarUpdate() {
      // Ensure enough bars are loaded
      if (CurrentBar < BarsRequiredToTrade)
        return;

      // Reset daily profit at the start of a new trading day
      if (Time[0].Date != lastTradeDate.Date) {
        lastTradeDate = Time[0].Date;
        dailyProfit = 0;
      }

      // Check if we're within trading hours
      TimeSpan currentTime = Time[0].TimeOfDay;
      if (currentTime < startTime || currentTime > endTime)
        return;

      // Check if we've hit profit/loss limits
      if (dailyProfit >= maxDailyProfit || dailyProfit <= -maxDailyLoss)
        return;

      // Calculate Williams %R
      double williamsRValue = WilliamsR(williamsRPeriod)[0];

      // Entry Logic
      if (williamsRValue > overboughtLevel && Position.MarketPosition != MarketPosition.Long) {
        EnterLong("LongRenko");
        SetTrailStop(CalculationMode.Ticks, TrailStopDistance);
        SetProfitTarget(CalculationMode.Ticks, ProfitTargetTicks);
      } else if (williamsRValue < oversoldLevel && Position.MarketPosition != MarketPosition.Short) {
        EnterShort("ShortRenko");
        SetTrailStop(CalculationMode.Ticks, TrailStopDistance);
        SetProfitTarget(CalculationMode.Ticks, ProfitTargetTicks);
      }

      // Exit Logic: Opposite Renko candle color
      if (Position.MarketPosition == MarketPosition.Long && Close[0] < Open[0]) {
        ExitLong("ExitLongRenko");
      } else if (Position.MarketPosition == MarketPosition.Short && Close[0] > Open[0]) {
        ExitShort("ExitShortRenko");
      }
    }

    protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time) {
      if (execution.Order != null && execution.Order.OrderState == OrderState.Filled) {
        // Calculate trade P/L based on market position and execution price
        double tradeProfit = 0;

        if (execution.Order.OrderAction == OrderAction.Buy || execution.Order.OrderAction == OrderAction.BuyToCover) {
          // Profit for closing a short or entering a long
          tradeProfit = (price - execution.Order.AverageFillPrice) * execution.Order.Quantity;
        } else if (execution.Order.OrderAction == OrderAction.Sell || execution.Order.OrderAction == OrderAction.SellShort) {
          // Profit for closing a long or entering a short
          tradeProfit = (execution.Order.AverageFillPrice - price) * execution.Order.Quantity;
        }

        // Convert ticks to monetary value
        tradeProfit *= Instrument.MasterInstrument.PointValue;

        // Update daily profit
        dailyProfit += tradeProfit;

        Print($"Trade P/L: ${tradeProfit:F2}, Daily P/L: ${dailyProfit:F2}");
      }
    }

    #region Properties
      [Display(Name = "Start Time", Description = "Trading session start time", Order = 1, GroupName = "Parameters")]
    public TimeSpan StartTime {
      get {
        return startTime;
      }
      set {
        startTime = value;
      }
    }

    [Display(Name = "End Time", Description = "Trading session end time", Order = 2, GroupName = "Parameters")]
    public TimeSpan EndTime {
      get {
        return endTime;
      }
      set {
        endTime = value;
      }
    }

    [Display(Name = "Max Daily Profit", Description = "Maximum profit allowed per day", Order = 3, GroupName = "Parameters")]
    public double MaxDailyProfit {
      get {
        return maxDailyProfit;
      }
      set {
        maxDailyProfit = value;
      }
    }

    [Display(Name = "Max Daily Loss", Description = "Maximum loss allowed per day", Order = 4, GroupName = "Parameters")]
    public double MaxDailyLoss {
      get {
        return maxDailyLoss;
      }
      set {
        maxDailyLoss = value;
      }
    }
    [Display(Name = "Trail Stop Distance (Ticks)", Description = "Trailing stop distance in ticks", Order = 5, GroupName = "Parameters")]
    public int TrailStopDistance {
      get {
        return trailStopDistance;
      }
      set {
        trailStopDistance = value;
      }
    }

    [Display(Name = "Profit Target (Ticks)", Description = "Profit target in ticks", Order = 6, GroupName = "Parameters")]
    public int ProfitTargetTicks {
      get {
        return profitTargetTicks;
      }
      set {
        profitTargetTicks = value;
      }
    }

    #endregion
  }
}
