﻿namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Bill Williams AC")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45396-bill-williams-ac")]
public class ACBW : Indicator
{
	#region Fields

	private readonly SMA _longSma = new();

	private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
	};

	private readonly SMA _shortSma = new();
	private readonly SMA _signalSma = new();

	private Color _negColor = DefaultColors.Purple;
	private Color _neutralColor = DefaultColors.Gray;
	private Color _posColor = DefaultColors.Green;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = "Positive", GroupName = "Drawing", Order = 610)]
	public System.Windows.Media.Color PosColor
	{
		get => _posColor.Convert();
		set
		{
			_posColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = "Negative", GroupName = "Drawing", Order = 620)]
	public System.Windows.Media.Color NegColor
	{
		get => _negColor.Convert();
		set
		{
			_negColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = "Neutral", GroupName = "Drawing", Order = 630)]
	public System.Windows.Media.Color NeutralColor
	{
		get => _neutralColor.Convert();
		set
		{
			_neutralColor = value.Convert();
			RecalculateValues();
		}
	}

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = "LongPeriod", GroupName = "Settings", Order = 100)]
	public int LongPeriod
	{
		get => _longSma.Period;
		set
		{
			if (value < 51 || value == ShortPeriod)
				return;

			_longSma.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = "ShortPeriod", GroupName = "Settings", Order = 110)]
	public int ShortPeriod
	{
		get => _shortSma.Period;
		set
		{
			if (value < 50 || value == LongPeriod)
				return;

			_shortSma.Period = value;

			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = "SignalPeriod", GroupName = "Settings", Order = 120)]
	public int SignalPeriod
	{
		get => _signalSma.Period;
		set
		{
			if (value < 50)
				return;

			_signalSma.Period = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public ACBW()
	{
		Panel = IndicatorDataProvider.NewPanel;

		_shortSma.Period = _signalSma.Period = 50;
		_longSma.Period = 51;

		DataSeries[0] = _renderSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var diff = _shortSma.Calculate(bar, value) - _longSma.Calculate(bar, value);
		var ac = diff - _signalSma.Calculate(bar, diff);
		_renderSeries[bar] = ac;

		if (bar == 0)
		{
			DataSeries.ForEach(x => x.Clear());
			return;
		}

		var prevValue = _renderSeries[bar - 1];

		if (ac > prevValue)
			_renderSeries.Colors[bar] = _posColor;
		else if (ac < prevValue)
			_renderSeries.Colors[bar] = _negColor;
		else
			_renderSeries.Colors[bar] = _posColor;
	}

	#endregion
}