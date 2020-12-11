﻿namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Directional Movement Index")]
	public class DmIndex : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dmDown = new ValueDataSeries("DmUp");
		private readonly ValueDataSeries _dmUp = new ValueDataSeries("DmDown");
		private readonly ValueDataSeries _downSeries = new ValueDataSeries(Resources.Down);

		private readonly ValueDataSeries _upSeries = new ValueDataSeries(Resources.Up);
		private readonly ATR _atr = new ATR();
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DmIndex()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 14;

			_upSeries.Color = Colors.Blue;
			_downSeries.Color = Colors.Red;
			Add(_atr);
			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			_dmUp[bar] = candle.High - prevCandle.High;
			_dmDown[bar] = prevCandle.Low - candle.Low;

			if (bar < _period)
				return;

			var dmUpSum = _dmUp.CalcSum(_period, bar);
			var dmDownSum = _dmDown.CalcSum(_period, bar);

			var smoothedUp = dmUpSum - dmUpSum / _period + _dmUp[bar];
			var smoothedDown = dmDownSum - dmDownSum / _period + _dmDown[bar];

			_upSeries[bar] = smoothedUp / _atr[bar] * 100m;
			_downSeries[bar] = smoothedDown / _atr[bar] * 100m;
		}

		#endregion
	}
}