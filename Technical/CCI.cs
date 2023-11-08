namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;

	using Utils.Common.Localization;

	[DisplayName("CCI")]
	[LocalizedDescription(typeof(Strings), nameof(Strings.CCI))]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/6854-cci")]
	public class CCI : Indicator
	{
		#region Fields

		private readonly SMA _sma = new();
		private readonly ValueDataSeries _typicalSeries = new("typical");
		private bool _drawLines = true;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Common),
			Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Show),
			GroupName = nameof(Strings.Line),
			Order = 30)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Contains(Line100))
						return;

					LineSeries.Add(Line100);
					LineSeries.Add(LineM100);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.p100),
			GroupName = nameof(Strings.Line),
			Order = 30)]
		public LineSeries Line100 { get; set; } = new("Line100", "100")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = 100,
			Width = 1,
			IsHidden = true
		};
		
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.m100),
			GroupName = nameof(Strings.Line),
			Order = 30)]
		public LineSeries LineM100 { get; set; } = new("LineM100", "-100")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = -100,
			Width = 1,
			IsHidden = true
		};

        #endregion

        #region ctor

        public CCI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Period = 10;
			
			LineSeries.Add(Line100);
			LineSeries.Add(LineM100);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			decimal mean = 0;
			var typical = (candle.High + candle.Low + candle.Close) / 3m;
			var sma0 = _sma.Calculate(bar, typical);
			_typicalSeries[bar] = typical;

			for (var i = bar; i > bar - Period && i >= 0; i--)
				mean += Math.Abs(_typicalSeries[i] - sma0);

			var res = 0.015m * (mean / Math.Min(Period, bar + 1));

			if (typical - sma0 == 0)
				this[bar] = 0.000000001m;
			else
				this[bar] = (typical - sma0) / (res <= 0.000000001m ? 1 : res);
		}

		#endregion
	}
}