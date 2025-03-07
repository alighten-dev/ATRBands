#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class ATRBands : Indicator
    {
        private ATR atr;  // instance of the ATR indicator

        #region Properties

        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        [Display(Name = "Offset", Order = 1, GroupName = "Parameters")]
        public double Offset { get; set; }

        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        [Display(Name = "Period", Order = 2, GroupName = "Parameters")]
        public int Period { get; set; }

        // Volatility Bar Color (with XML serialization workaround)
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Volatility Bar Color", Order = 3, GroupName = "Parameters")]
        public Brush VolatilityBarColor { get; set; }
        [Browsable(false)]
        public string VolatilityBarColorSerialize
        {
			get { return Serialize.BrushToString(VolatilityBarColor); }
   			set { VolatilityBarColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Bands", Order = 4, GroupName = "Parameters")]
        public bool ShowBands { get; set; }

        // Enable bar coloring toggle
        [NinjaScriptProperty]
        [Display(Name = "Enable Bar Coloring", Order = 5, GroupName = "Parameters")]
        public bool EnableBarPainting { get; set; }

        // Upper Band Color with XML workaround
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Upper Band Color", Order = 6, GroupName = "Parameters")]
        public Brush UpperBandColor { get; set; } = Brushes.Green;
        [Browsable(false)]
        public string UpperBandColorSerialize
        {
			get { return Serialize.BrushToString(UpperBandColor); }
   			set { UpperBandColor = Serialize.StringToBrush(value); }
        }

        // Lower Band Color with XML workaround
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Lower Band Color", Order = 7, GroupName = "Parameters")]
        public Brush LowerBandColor { get; set; } = Brushes.Red;
        [Browsable(false)]
        public string LowerBandColorSerialize
        {
			get { return Serialize.BrushToString(LowerBandColor); }
   			set { LowerBandColor = Serialize.StringToBrush(value); }
        }

        // Diamond Signal properties:

        // Show Diamond Signal toggle
        [NinjaScriptProperty]
        [Display(Name = "Show Diamond Signal", Order = 8, GroupName = "Parameters")]
        public bool ShowDiamondSignal { get; set; } = true;

        // Short Signal Color (for when high > upper band)
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Short Signal Color", Order = 9, GroupName = "Short Signal")]
        public Brush ShortSignalColor { get; set; } = Brushes.Magenta;
        [Browsable(false)]
        public string ShortSignalColorSerialize
        {
			get { return Serialize.BrushToString(ShortSignalColor); }
   			set { ShortSignalColor = Serialize.StringToBrush(value); }
        }

        // Short Signal Offset (ticks)
        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Short Signal Offset (ticks)", Order = 10, GroupName = "Short Signal")]
        public double ShortSignalOffset { get; set; } = 20;

        // Long Signal Color (for when low < lower band)
        [XmlIgnore]
        [NinjaScriptProperty]
        [Display(Name = "Long Signal Color", Order = 11, GroupName = "Long Signal")]
        public Brush LongSignalColor { get; set; } = Brushes.Magenta;
        [Browsable(false)]
        public string LongSignalColorSerialize
        {
			get { return Serialize.BrushToString(LongSignalColor); }
   			set { LongSignalColor = Serialize.StringToBrush(value); }
        }

        // Long Signal Offset (ticks)
        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Long Signal Offset (ticks)", Order = 12, GroupName = "Long Signal")]
        public double LongSignalOffset { get; set; } = 20;

        #endregion

        #region DataSeries
        // Transparent signal series:
        // +1 = long signal (bar low is below lower band)
        // -1 = short signal (bar high is above upper band)
        // 0 = no signal
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> SignalValue
        {
            get { return Values[0]; }
        }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"ATR Bands indicator with configurable band colors, optional bar coloring, and diamond signals. - By Alighten";
                Name = "ATRBands";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Define plots in this order:
                // Plot 0: SignalValue (transparent for strategy access)
                // Plot 1: UpperBand
                // Plot 2: LowerBand
                AddPlot(Brushes.Transparent, "SignalValue");
                AddPlot(UpperBandColor, "UpperBand");
                AddPlot(LowerBandColor, "LowerBand");

                // Default values
                Offset = 2;
                Period = 60;
                VolatilityBarColor = Brushes.Yellow;
                ShowBands = true;
                EnableBarPainting = true;
                ShowDiamondSignal = true;
            }
            else if (State == State.Configure)
            {
                // No additional configuration.
            }
            else if (State == State.DataLoaded)
            {
                // Initialize the ATR indicator with the defined period.
                atr = ATR(Period);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Period)
                return;

            // Calculate the ATR-based bands.
            double upperBand = Close[0] + Offset * atr[0];
            double lowerBand = Close[0] - Offset * atr[0];

            // Plot bands if enabled.
            if (ShowBands)
            {
                Values[1][0] = upperBand;
                Values[2][0] = lowerBand;
            }
            else
            {
                Values[1][0] = double.NaN;
                Values[2][0] = double.NaN;
            }

            // Determine the signal.
            double signal = 0;
            if (ShowDiamondSignal)
            {
                // Issue short signal if bar high is above the upper band.
                if (High[0] > upperBand)
                {
                    signal = -1;
                    double diamondY = High[0] + (TickSize * ShortSignalOffset);
                    Draw.Diamond(this, "ShortSignal" + CurrentBar, false, 0, diamondY, ShortSignalColor);
                }
                // Issue long signal if bar low is below the lower band.
                else if (Low[0] < lowerBand)
                {
                    signal = 1;
                    double diamondY = Low[0] - (TickSize * LongSignalOffset);
                    Draw.Diamond(this, "LongSignal" + CurrentBar, false, 0, diamondY, LongSignalColor);
                }
            }
            Values[0][0] = signal;

            // Optional bar coloring.
            if (EnableBarPainting)
            {
                if (High[0] >= upperBand || Low[0] <= lowerBand)
                {
                    BarBrush = VolatilityBarColor;
                    CandleOutlineBrush = VolatilityBarColor;
                }
                else
                {
                    BarBrush = null;
                    CandleOutlineBrush = null;
                }
            }
            else
            {
                BarBrush = null;
                CandleOutlineBrush = null;
            }
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ATRBands[] cacheATRBands;
		public ATRBands ATRBands(double offset, int period, Brush volatilityBarColor, bool showBands, bool enableBarPainting, Brush upperBandColor, Brush lowerBandColor, bool showDiamondSignal, Brush shortSignalColor, double shortSignalOffset, Brush longSignalColor, double longSignalOffset)
		{
			return ATRBands(Input, offset, period, volatilityBarColor, showBands, enableBarPainting, upperBandColor, lowerBandColor, showDiamondSignal, shortSignalColor, shortSignalOffset, longSignalColor, longSignalOffset);
		}

		public ATRBands ATRBands(ISeries<double> input, double offset, int period, Brush volatilityBarColor, bool showBands, bool enableBarPainting, Brush upperBandColor, Brush lowerBandColor, bool showDiamondSignal, Brush shortSignalColor, double shortSignalOffset, Brush longSignalColor, double longSignalOffset)
		{
			if (cacheATRBands != null)
				for (int idx = 0; idx < cacheATRBands.Length; idx++)
					if (cacheATRBands[idx] != null && cacheATRBands[idx].Offset == offset && cacheATRBands[idx].Period == period && cacheATRBands[idx].VolatilityBarColor == volatilityBarColor && cacheATRBands[idx].ShowBands == showBands && cacheATRBands[idx].EnableBarPainting == enableBarPainting && cacheATRBands[idx].UpperBandColor == upperBandColor && cacheATRBands[idx].LowerBandColor == lowerBandColor && cacheATRBands[idx].ShowDiamondSignal == showDiamondSignal && cacheATRBands[idx].ShortSignalColor == shortSignalColor && cacheATRBands[idx].ShortSignalOffset == shortSignalOffset && cacheATRBands[idx].LongSignalColor == longSignalColor && cacheATRBands[idx].LongSignalOffset == longSignalOffset && cacheATRBands[idx].EqualsInput(input))
						return cacheATRBands[idx];
			return CacheIndicator<ATRBands>(new ATRBands(){ Offset = offset, Period = period, VolatilityBarColor = volatilityBarColor, ShowBands = showBands, EnableBarPainting = enableBarPainting, UpperBandColor = upperBandColor, LowerBandColor = lowerBandColor, ShowDiamondSignal = showDiamondSignal, ShortSignalColor = shortSignalColor, ShortSignalOffset = shortSignalOffset, LongSignalColor = longSignalColor, LongSignalOffset = longSignalOffset }, input, ref cacheATRBands);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ATRBands ATRBands(double offset, int period, Brush volatilityBarColor, bool showBands, bool enableBarPainting, Brush upperBandColor, Brush lowerBandColor, bool showDiamondSignal, Brush shortSignalColor, double shortSignalOffset, Brush longSignalColor, double longSignalOffset)
		{
			return indicator.ATRBands(Input, offset, period, volatilityBarColor, showBands, enableBarPainting, upperBandColor, lowerBandColor, showDiamondSignal, shortSignalColor, shortSignalOffset, longSignalColor, longSignalOffset);
		}

		public Indicators.ATRBands ATRBands(ISeries<double> input , double offset, int period, Brush volatilityBarColor, bool showBands, bool enableBarPainting, Brush upperBandColor, Brush lowerBandColor, bool showDiamondSignal, Brush shortSignalColor, double shortSignalOffset, Brush longSignalColor, double longSignalOffset)
		{
			return indicator.ATRBands(input, offset, period, volatilityBarColor, showBands, enableBarPainting, upperBandColor, lowerBandColor, showDiamondSignal, shortSignalColor, shortSignalOffset, longSignalColor, longSignalOffset);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ATRBands ATRBands(double offset, int period, Brush volatilityBarColor, bool showBands, bool enableBarPainting, Brush upperBandColor, Brush lowerBandColor, bool showDiamondSignal, Brush shortSignalColor, double shortSignalOffset, Brush longSignalColor, double longSignalOffset)
		{
			return indicator.ATRBands(Input, offset, period, volatilityBarColor, showBands, enableBarPainting, upperBandColor, lowerBandColor, showDiamondSignal, shortSignalColor, shortSignalOffset, longSignalColor, longSignalOffset);
		}

		public Indicators.ATRBands ATRBands(ISeries<double> input , double offset, int period, Brush volatilityBarColor, bool showBands, bool enableBarPainting, Brush upperBandColor, Brush lowerBandColor, bool showDiamondSignal, Brush shortSignalColor, double shortSignalOffset, Brush longSignalColor, double longSignalOffset)
		{
			return indicator.ATRBands(input, offset, period, volatilityBarColor, showBands, enableBarPainting, upperBandColor, lowerBandColor, showDiamondSignal, shortSignalColor, shortSignalOffset, longSignalColor, longSignalOffset);
		}
	}
}

#endregion
