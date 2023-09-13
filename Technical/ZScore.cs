﻿namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Z-Score")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45325-z-score")]
	public class ZScore : Indicator
	{
		#region Fields

		private readonly SMA _sma = new() { Period = 10 };
		private readonly StdDev _stdDev = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "SMA", GroupName = "Period", Order = 100)]
		[Range(1, 10000)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "StdDev", GroupName = "Period", Order = 110)]
		[Range(1, 10000)]
        public int StdPeriod
		{
			get => _stdDev.Period;
			set
			{
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ZScore()
		{
			Panel = IndicatorDataProvider.NewPanel;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_stdDev.Calculate(bar, value);

			this[bar] = _stdDev[bar] != 0
				? (value - _sma[bar]) / _stdDev[bar]
				: 0;
		}

		#endregion
	}
}