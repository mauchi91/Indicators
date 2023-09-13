namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;
    using Utils.Common.Localization;

	[DisplayName("Standard Deviation")]
	[LocalizedDescription(typeof(Strings), "StdDev")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7208-stddev")]
	public class StdDev : Indicator
	{
		#region Fields

		private readonly SMA _sma = new() { Period = 10 };

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
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

		public StdDev()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].UseMinimizedModeIfEnabled = true;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var sma = _sma.Calculate(bar, value);

			var start = Math.Max(0, bar - Period + 1);
			var count = Math.Min(bar + 1, Period);

			var sum = 0m;

			for (var i = start; i < start + count; i++)
			{
				var tmp = Math.Abs((decimal)SourceDataSeries[i] - sma);
				sum += tmp * tmp;
			}
			
			this[bar] = (decimal)Math.Sqrt((double)(sum / count));
		}

		#endregion
	}
}