﻿namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Collections;

[DisplayName("Active Volume")]
[Category("3rd party addons")]
public class ActiveVolume : Indicator
{
	#region Nested types

	[Serializable]
	public enum CalcMode
	{
        [Display(ResourceType = typeof(Strings), Name = "BidAsk")]
        BidAsk = 0,

        [Display(ResourceType = typeof(Strings), Name = "Bid")]
        Bid = 1,

        [Display(ResourceType = typeof(Strings), Name = "Ask")]
        Ask = 2
	}

	#endregion

	#region Fields

	private readonly RenderStringFormat _renderStringFormat = new() 
	{
		Alignment = StringAlignment.Center,
		LineAlignment = StringAlignment.Center,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private Dictionary<decimal, decimal> _askValues = new();
	private Dictionary<decimal, decimal> _bidValues = new();
	private List<CumulativeTrade> _cumulativeTrades = new();
	private DateTime _dateTimeFrom = DateTime.UtcNow.Date;

	private int _filter = 50;
	private CumulativeTrade _lastTrade = new();
	private object _locker = new();
	private decimal _maxAskValue;
	private decimal _maxBidAskValue;
	private decimal _maxBidValue;
	private bool _reloadTrades;

	private RenderFont _warningFont = new("Arial", 20);

	#endregion

	#region Properties
	[Range(0, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), Name = "Filter", GroupName = "Settings", Order = 10)]
	public int Filter
	{
		get => _filter;
		set
		{
			_filter = value;
			Calculate();
		}
	}

	[Range(0, 500)]
	[Display(ResourceType = typeof(Strings), Name = "RowWidth", GroupName = "Settings", Order = 30)]
	public int RowWidth { get; set; } = 70;

	[Display(ResourceType = typeof(Strings), Name = "ShowBid", GroupName = "Settings", Order = 40)]
	public bool ShowBid { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = "ShowAsk", GroupName = "Settings", Order = 50)]
	public bool ShowAsk { get; set; } = true;

	[Display(ResourceType = typeof(String	), Name = "ShowVolume", GroupName = "Settings", Order = 60)]
	public bool ShowSum { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = "Offset", GroupName = "Settings", Order = 70)]
	public int Offset { get; set; }

	[Display(ResourceType = typeof(Strings), Name = "SessionBegin", GroupName = "Settings", Order = 80)]
	public DateTime DateFrom
	{
		get => _dateTimeFrom;
		set
		{
			_dateTimeFrom = value;
			_reloadTrades = true;
			RecalculateValues();
		}
	}

	[Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = "DigitsAfterComma", GroupName = "Settings", Order = 90)]
    public int DigitsAfterComma { get; set; }

    [Display(ResourceType = typeof(Strings), Name = "CalculationMode", GroupName = "Profile", Order = 10)]
	public CalcMode Mode { get; set; }

	[Range(0, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), Name = "Width", GroupName = "Profile", Order = 20)]
	public int ProfileWidth { get; set; } = 70;

	[Display(ResourceType = typeof(Strings), Name = "Offset", GroupName = "Profile", Order = 30)]
	public int ProfileOffset { get; set; }

	[Display(ResourceType = typeof(Strings), Name = "BackGround", GroupName = "Profile", Order = 40)]
	public Color ProfileFillColor { get; set; } = Color.White;

	[Display(ResourceType = typeof(Strings), Name = "BidColor", GroupName = "Profile", Order = 50)]
	public Color BidProfileValueColor { get; set; } = Color.Green;

	[Display(ResourceType = typeof(Strings), Name = "AskColor", GroupName = "Profile", Order = 60)]
	public Color AskProfileValueColor { get; set; } = Color.Red;

	[Display(Name = "Creator", GroupName = "Info", Order = 10)]
	public string Creator => "Aleksey Ivanov";

	#endregion

	#region ctor

	public ActiveVolume()
		: base(true)
	{
		DataSeries[0].IsHidden = true;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
		DrawAbovePrice = true;
	}

	#endregion

	#region Protected methods

	protected override void OnInitialize()
	{
		_reloadTrades = true;
	}

	protected override void OnDispose()
	{
		_cumulativeTrades.Clear();
		_cumulativeTrades.TrimExcess();
	}

	protected override void OnCalculate(int bar, decimal value)
	{
	}

	protected override void OnFinishRecalculate()
	{
		if (InstrumentInfo is null || !_reloadTrades)
			return;

		if (CurrentBar <= 0)
			return;

		var firstTime = GetCandle(0).Time;
		var lastTime = GetCandle(CurrentBar - 1).LastTime;

		var startTime = new DateTime(
			Math.Max(_dateTimeFrom.Ticks, firstTime.Ticks));

		startTime = new DateTime(
			Math.Max(startTime.Ticks, DateTime.UtcNow.AddDays(-5).Date.Ticks));

		var request = new CumulativeTradesRequest(startTime, lastTime, 0, 0);
		RequestForCumulativeTrades(request);
	}

	protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
	{
		_reloadTrades = false;
		_cumulativeTrades = cumulativeTrades.ToList();
		Calculate();
	}

	protected override void OnCumulativeTrade(CumulativeTrade trade)
	{
		if (ChartInfo is null)
			return;

		if (CurrentBar <= 0)
			return;

		_cumulativeTrades.Add(trade);

		if (trade.NewBid.Volume < _filter && trade.NewAsk.Volume < _filter)
			return;

		var bidAskValue = 0m;

		lock (_locker)
		{
			switch (trade.Direction)
			{
				case TradeDirection.Sell:
				{
					if (_bidValues.ContainsKey(trade.FirstPrice))
						_bidValues[trade.FirstPrice] += trade.Volume;
					else
						_bidValues.Add(trade.FirstPrice, trade.Volume);
					_maxBidValue = Math.Max(_maxBidValue, _bidValues[trade.FirstPrice]);

					bidAskValue = _bidValues[trade.FirstPrice];

					if (_askValues.TryGetValue(trade.FirstPrice, out var askValue))
						bidAskValue += askValue;
					break;
				}
				case TradeDirection.Buy:
				{
					if (_askValues.ContainsKey(trade.FirstPrice))
						_askValues[trade.FirstPrice] += trade.Volume;
					else
						_askValues.Add(trade.FirstPrice, trade.Volume);
					_maxAskValue = Math.Max(_maxAskValue, _askValues[trade.FirstPrice]);

					bidAskValue = _askValues[trade.FirstPrice];

					if (_bidValues.TryGetValue(trade.FirstPrice, out var bidValue))
						bidAskValue += bidValue;
					break;
				}
			}
		}

		_lastTrade = trade;

		_maxBidAskValue = Math.Max(_maxBidAskValue, bidAskValue);
	}

	protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
	{
		if (ChartInfo is null)
			return;

		if (CurrentBar <= 0 || _cumulativeTrades.Count == 0)
			return;

		_cumulativeTrades.RemoveAt(_cumulativeTrades.Count - 1);
		_cumulativeTrades.Add(trade);

		var containsTrade = _lastTrade.IsEqual(trade);

		if (trade.NewBid.Volume < _filter && trade.NewAsk.Volume < _filter)
			return;

		var incValue = containsTrade
			? trade.Volume - _lastTrade.Volume
			: trade.Volume;

		var bidAskValue = 0m;

		lock (_locker)
		{
			switch (trade.Direction)
			{
				case TradeDirection.Sell:
				{
					_bidValues.GetOrAdd(trade.FirstPrice, _ => 0);

					_bidValues[trade.FirstPrice] += incValue;

					_maxBidValue = Math.Max(_maxBidValue, _bidValues[trade.FirstPrice]);

					bidAskValue = _bidValues[trade.FirstPrice];

					if (_askValues.TryGetValue(trade.FirstPrice, out var askValue))
						bidAskValue += askValue;
					break;
				}
				case TradeDirection.Buy:
				{
					_askValues.GetOrAdd(trade.FirstPrice, _ => 0);

					_askValues[trade.FirstPrice] += incValue;

					_maxAskValue = Math.Max(_maxAskValue, _askValues[trade.FirstPrice]);

					bidAskValue = _askValues[trade.FirstPrice];

					if (_bidValues.TryGetValue(trade.FirstPrice, out var bidValue))
						bidAskValue += bidValue;
					break;
				}
			}
		}

		_lastTrade = trade;

		_maxBidAskValue = Math.Max(_maxBidAskValue, bidAskValue);
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo == null)
			return;

		var rowHeight = (int)ChartInfo.PriceChartContainer.PriceRowHeight;

		var fontSize = (int)Math.Min(rowHeight / 2m, 14);
		var renderFont = new RenderFont("Arial", fontSize);

		decimal low, high;

		lock (_locker)
		{
			low = Math.Min(_askValues.Keys.DefaultIfEmpty(0).Min(), _bidValues.Keys.DefaultIfEmpty(0).Min());
			high = Math.Max(_askValues.Keys.DefaultIfEmpty(0).Max(), _bidValues.Keys.DefaultIfEmpty(0).Max());
		}

		low = Math.Max(low, ChartInfo.PriceChartContainer.Low);
		high = Math.Min(high, ChartInfo.PriceChartContainer.High);

		var yHigh = ChartInfo.GetYByPrice(high);
		var yLow = ChartInfo.GetYByPrice(low - InstrumentInfo.TickSize);

		context.FillRectangle(ProfileFillColor, new Rectangle(ProfileOffset, yHigh, ProfileWidth, yLow - yHigh));

		var drawTable = rowHeight >= 8;

		if (!drawTable)
		{
			var shift = 10;
			var text = Strings.TooSmallRows;
			var textArray = text.Split(' ');
			var textPart1 = $"{string.Join(' ', textArray.Take(3))}\n";
			var textPart2 = string.Join(' ', textArray.Skip(3));
			
			text = $"{textPart1}{textPart2}";
			var textSize = context.MeasureString(text, _warningFont);

			var tableRect = new Rectangle(ProfileOffset + ProfileWidth, yHigh, textSize.Width + shift, Math.Max(textSize.Height + shift, yLow - yHigh));
			context.FillRectangle(Color.White, tableRect);
			context.DrawRectangle(RenderPens.Blue, tableRect);
			context.DrawString(text, _warningFont, Color.Black, tableRect, _renderStringFormat);
		}

		for (var price = low; price <= high; price += InstrumentInfo.TickSize)
		{
			var y1 = ChartInfo.GetYByPrice(price);
			var sum = 0m;

			decimal askValue, bidValue;

			lock (_locker)
			{
				if (_bidValues.TryGetValue(price, out bidValue))
					sum += _bidValues[price];

				if (_askValues.TryGetValue(price, out askValue))
					sum += _askValues[price];
			}

			switch (Mode)
			{
				case CalcMode.BidAsk:
					DrawBidAsk(context, bidValue, askValue, y1, rowHeight);
					break;
				case CalcMode.Bid:
					DrawBid(context, bidValue, y1, rowHeight);
					break;
				case CalcMode.Ask:
					DrawAsk(context, askValue, y1, rowHeight);
					break;
			}

			if (!drawTable)
				continue;

			var x = ProfileWidth + Offset;

			var priceRect = new Rectangle(x, y1, RowWidth, rowHeight);
			context.FillRectangle(Color.White, priceRect);
			context.DrawRectangle(RenderPens.Blue, priceRect);
			context.DrawString(price.ToString(CultureInfo.InvariantCulture), renderFont, Color.Black, priceRect, _renderStringFormat);

			x += RowWidth;
			string text;

			if (ShowBid)
			{
                text = GetRoundedValueString(bidValue);
                var rect = new Rectangle(x, y1, RowWidth, rowHeight);
				context.FillRectangle(Color.White, rect);
				context.DrawRectangle(RenderPens.Blue, rect);
				context.DrawString(text, renderFont, Color.Black, rect, _renderStringFormat);

				x += RowWidth;
			}

			if (ShowAsk)
			{
                text = GetRoundedValueString(askValue);
                var rect = new Rectangle(x, y1, RowWidth, rowHeight);
				context.FillRectangle(Color.White, rect);
				context.DrawRectangle(RenderPens.Blue, rect);
				context.DrawString(text, renderFont, Color.Black, rect, _renderStringFormat);

				x += RowWidth;
			}

			if (ShowSum)
			{
                text = GetRoundedValueString(sum);
                var rect = new Rectangle(x, y1, RowWidth, rowHeight);
				context.FillRectangle(Color.White, rect);
				context.DrawRectangle(RenderPens.Blue, rect);

				context.DrawString(text, renderFont, Color.Black, rect, _renderStringFormat);
			}
		}
	}

    #endregion

    #region Private methods

    private string GetRoundedValueString(decimal value)
    {
        return Math.Round(value, DigitsAfterComma).ToString(CultureInfo.InvariantCulture);
    }

    private void DrawBidAsk(RenderContext context, decimal bidValue, decimal askValue, int y, int height)
	{
		var askProfileValue = 0;
		var bidProfileValue = 0;

		if (_maxBidAskValue > 0)
		{
			bidProfileValue = (int)Math.Min(bidValue * ProfileWidth / _maxBidAskValue, ProfileWidth - 1);
			askProfileValue = (int)Math.Min(askValue * ProfileWidth / _maxBidAskValue, ProfileWidth - 1);
		}

		context.FillRectangle(AskProfileValueColor,
			new Rectangle(ProfileOffset + bidProfileValue, y, askProfileValue, height));

		context.FillRectangle(BidProfileValueColor,
			new Rectangle(ProfileOffset, y, bidProfileValue, height));
	}

	private void DrawBid(RenderContext context, decimal bidValue, int y, int height)
	{
		var bidProfileValue = 0m;

		if (_maxBidValue > 0)
			bidProfileValue = Math.Min(bidValue * ProfileWidth / _maxBidValue, ProfileWidth - 1);

		context.FillRectangle(BidProfileValueColor,
			new Rectangle(ProfileOffset, y, (int)bidProfileValue, height));
	}

	private void DrawAsk(RenderContext context, decimal askValue, int y, int height)
	{
		var askProfileValue = 0m;

		if (_maxAskValue > 0)
			askProfileValue = Math.Min(askValue * ProfileWidth / _maxAskValue, ProfileWidth - 1);

		context.FillRectangle(AskProfileValueColor,
			new Rectangle(ProfileOffset, y, (int)askProfileValue, height));
	}

	private void Calculate()
	{
		lock (_locker)
		{
			_bidValues.Clear();
			_askValues.Clear();
		}

		_maxAskValue = _maxBidValue = _maxBidAskValue = 0;

		foreach (var ct in _cumulativeTrades.Where(x => x.NewBid.Volume >= _filter || x.NewAsk.Volume >= _filter))
		{
			var bidAskValue = 0m;

			lock (_locker)
			{
				switch (ct.Direction)
				{
					case TradeDirection.Sell:
						_bidValues.GetOrAdd(ct.FirstPrice, _ => 0);

						_bidValues[ct.FirstPrice] += ct.Volume;

						_maxBidValue = Math.Max(_maxBidValue, _bidValues[ct.FirstPrice]);

						bidAskValue = _bidValues[ct.FirstPrice];

						if (_askValues.TryGetValue(ct.FirstPrice, out var askValue))
							bidAskValue += askValue;
						break;

					case TradeDirection.Buy:
						_askValues.GetOrAdd(ct.FirstPrice, _ => 0);

						_askValues[ct.FirstPrice] += ct.Volume;

						_maxAskValue = Math.Max(_maxAskValue, _askValues[ct.FirstPrice]);

						bidAskValue = _askValues[ct.FirstPrice];

						if (_bidValues.TryGetValue(ct.FirstPrice, out var bidValue))
							bidAskValue += bidValue;
						break;
				}
			}

			_maxBidAskValue = Math.Max(_maxBidAskValue, bidAskValue);
		}
	}

	#endregion
}