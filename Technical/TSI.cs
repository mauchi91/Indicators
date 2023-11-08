﻿namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [HelpLink("https://support.atas.net/knowledge-bases/2/articles/53501-true-strength-index")]
	[DisplayName("True Strength Index")]
	public class TSI : Indicator
	{
		#region Fields

		private readonly EMA _absEma = new() { Period = 13 };
        private readonly EMA _absSecEma = new() { Period = 25 };
        private readonly EMA _ema = new() { Period = 13 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Values)
		{
			Color = DefaultColors.Blue.Convert(),
			VisualType = VisualMode.Histogram
		};
		private readonly ValueDataSeries _renderSmoothedSeries = new("RenderSmoothedSeries", Strings.Smooth) { IgnoredByAlerts = true };
		private readonly EMA _secEma = new() { Period = 25 };
        private readonly EMA _smoothEma = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod1), GroupName = nameof(Strings.Settings), Order = 100)]
		public int EmaPeriod
		{
			get => _ema.Period;
			set
			{
				_ema.Period = _absEma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod2), GroupName = nameof(Strings.Settings), Order = 110)]
		public int EmaSecPeriod
		{
			get => _secEma.Period;
			set
			{
				_secEma.Period = _absSecEma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Smooth), GroupName = nameof(Strings.Settings), Order = 120)]
		public int SmoothPeriod
		{
			get => _smoothEma.Period;
			set
			{
				_smoothEma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public TSI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _renderSeries;
			DataSeries.Add(_renderSmoothedSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var diff = candle.Close - prevCandle.Close;
			_ema.Calculate(bar, diff);
			_secEma.Calculate(bar, _ema[bar]);

			_absEma.Calculate(bar, Math.Abs(diff));
			_absSecEma.Calculate(bar, _absEma[bar]);

			_renderSeries[bar] = 100 * _secEma[bar] / _absSecEma[bar];
			_renderSmoothedSeries[bar] = _smoothEma.Calculate(bar, _renderSeries[bar]);
		}

		#endregion
	}
}