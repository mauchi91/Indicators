﻿namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Inertia")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45246-inertia")]
	public class Inertia : Indicator
	{
		#region Fields

		private readonly LinearReg _linReg = new() { Period = 14 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly RVI2 _rvi = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RVI), GroupName = nameof(Strings.Period), Order = 100)]
		[Range(1, 10000)]
        public int RviPeriod
		{
			get => _rvi.Period;
			set
			{
				_rvi.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LinearReg), GroupName = nameof(Strings.Period), Order = 110)]
		[Range(1, 10000)]
        public int LinearRegPeriod
		{
			get => _linReg.Period;
			set
			{
				_linReg.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Inertia()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			Add(_rvi);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _linReg.Calculate(bar, _rvi[bar]);
		}

		#endregion
	}
}