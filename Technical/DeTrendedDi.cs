﻿namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Detrended Oscillator - DiNapoli")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45487-detrended-oscillator-dinapoli")]
	public class DeTrendedDi : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly SMA _sma = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeTrendedDi()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = value - _sma.Calculate(bar, value);
		}

		#endregion
	}
}