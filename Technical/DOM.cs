﻿namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using Newtonsoft.Json;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Helpers;
using OFT.Rendering.Tools;

using Utils.Common;
using Utils.Common.Logging;

using Color = System.Drawing.Color;

[Category("Other")]
[DisplayName("Depth Of Market")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/352-depth-of-market")]
public class DOM : Indicator
{
	#region Nested types

	public class VolumeInfo
	{
		#region Properties

		public decimal Volume { get; set; }

		public decimal Price { get; set; }

		#endregion
	}

	public enum Mode
	{
		[Display(ResourceType = typeof(Resources), Name = "Levels")]
		Common,

		[Display(ResourceType = typeof(Resources), Name = "Cumulative")]
		Cumulative,

		[Display(ResourceType = typeof(Resources), Name = "Both")]
		Combined
	}

	#endregion

	#region Static and constants

	private const int _fontSize = 10;
	private const int _unitedVolumeHeight = 15;
	private const int _heightToSolidMode = 4;

	#endregion

	#region Fields

	private readonly ValueDataSeries _downScale = new("Down");

	private readonly RedrawArg _emptyRedrawArg = new(new Rectangle(0, 0, 0, 0));

	private readonly RenderStringFormat _stringLeftFormat = new()
	{
		Alignment = StringAlignment.Near,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private readonly RenderStringFormat _stringRightFormat = new()
	{
		Alignment = StringAlignment.Far,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private readonly ValueDataSeries _upScale = new("Up");
	
	private Color _askBackGround;

	private Color _askColor;
	private HistogramRender _asksHistogram;
	private Color _bestAskBackGround;
	private Color _bestBidBackGround;
	private Color _bidBackGround;
	private Color _bidColor;
	private HistogramRender _bidsHistogram;

	private SortedList<decimal, decimal> _cumulativeAsk = new();
	private SortedList<decimal, decimal> _cumulativeBid = new();

	private MultiColorsHistogramRender _cumulativeHistogram;
	private string _digitFormat = "0.#####";
	private int _digitsAfterComma = 5;
	private Dictionary<decimal, Color> _filteredColors = new();

	private RenderFont _font = new("Arial", _fontSize);
	private object _locker = new();

	private decimal _maxBid;
	private decimal _maxPrice;

	private VolumeInfo _maxVolume = new();

	private SortedList<decimal, MarketDataArg> _mDepth = new();
	private decimal _minAsk;
	private decimal _minPrice;

	private int _priceLevelsHeight;
	private int _scale;
	private Color _textColor;
	private Mode _visualMode = Mode.Common;
	private Color _volumeAskColor;
	private Color _volumeBidColor;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "HistogramSize", Order = 100)]
	public Mode VisualMode
	{
		get => _visualMode;
		set
		{
			_visualMode = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "UseAutoSize", GroupName = "HistogramSize", Order = 105)]
	public bool UseAutoSize { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "ProportionVolume", GroupName = "HistogramSize", Order = 110)]
	[Range(0, 1000000000000)]
	public decimal ProportionVolume { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "HistogramSize", Order = 120)]
	[Range(0, 4000)]
	public int Width { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "RightToLeft", GroupName = "HistogramSize", Order = 130)]
	public bool RightToLeft { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "DigitsAfterComma", GroupName = "Visualization", Order = 150)]
	[Range(0, 10)]
	public int DigitsAfterComma
	{
		get => _digitsAfterComma;
		set
		{
			_digitsAfterComma = value;

			var format = "0.";

			for (var i = 0; i < value; i++)
				format += '#';

			_digitFormat = format;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "BidRows", GroupName = "LevelsMode", Order = 200)]
	public System.Windows.Media.Color BidRows
	{
		get => _bidColor.Convert();
		set
		{
			_bidColor = value.Convert();
			_volumeBidColor = Color.FromArgb(50, value.R, value.G, value.B);
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "LevelsMode", Order = 210)]
	public System.Windows.Media.Color TextColor
	{
		get => _textColor.Convert();
		set => _textColor = value.Convert();
	}

	[Display(ResourceType = typeof(Resources), Name = "AskRows", GroupName = "LevelsMode", Order = 220)]
	public System.Windows.Media.Color AskRows
	{
		get => _askColor.Convert();
		set
		{
			_askColor = value.Convert();
			_volumeAskColor = Color.FromArgb(50, value.R, value.G, value.B);
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "BidsBackGround", GroupName = "LevelsMode", Order = 230)]
	public System.Windows.Media.Color BidsBackGround
	{
		get => _bidBackGround.Convert();
		set => _bidBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Resources), Name = "AsksBackGround", GroupName = "LevelsMode", Order = 240)]
	public System.Windows.Media.Color AsksBackGround
	{
		get => _askBackGround.Convert();
		set => _askBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Resources), Name = "BestBidBackGround", GroupName = "LevelsMode", Order = 250)]
	public System.Windows.Media.Color BestBidBackGround
	{
		get => _bestBidBackGround.Convert();
		set => _bestBidBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Resources), Name = "BestAskBackGround", GroupName = "LevelsMode", Order = 260)]
	public System.Windows.Media.Color BestAskBackGround
	{
		get => _bestAskBackGround.Convert();
		set => _bestAskBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Resources), Name = "Filters", GroupName = "LevelsMode", Order = 270)]
	[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Reuse)]
	public ObservableCollection<FilterColor> FilterColors { get; set; } = new();

	[Display(ResourceType = typeof(Resources), Name = "AskColor", GroupName = "CumulativeMode", Order = 280)]
	public Color CumulativeAskColor { get; set; } = Color.FromArgb(255, 100, 100);

	[Display(ResourceType = typeof(Resources), Name = "BidColor", GroupName = "CumulativeMode", Order = 285)]
	public Color CumulativeBidColor { get; set; } = Color.FromArgb(100, 255, 100);

	[Display(ResourceType = typeof(Resources), Name = "ShowCumulativeValues", GroupName = "Other", Order = 300)]
	public bool ShowCumulativeValues { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "CustomPriceLevelsHeight", GroupName = "Other", Order = 310)]
	public int PriceLevelsHeight
	{
		get => _priceLevelsHeight;
		set
		{
			if (value < 0)
				return;

			_priceLevelsHeight = value;
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "UseScale", GroupName = "Scale", Order = 400)]
	public bool UseScale
	{
		get => _upScale.ScaleIt;
		set
		{
			_upScale.ScaleIt = _downScale.ScaleIt = value;
			_upScale.Clear();
			_downScale.Clear();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "CustomScale", GroupName = "Scale", Order = 410)]
	[Range(0, 1000)]
	public int Scale
	{
		get => _scale;
		set
		{
			_scale = value;
			_upScale.Clear();
			_downScale.Clear();
		}
	}

	#endregion

	#region ctor

	public DOM()
		: base(true)
	{
		DrawAbovePrice = true;
		DenyToChangePanel = true;
		_upScale.IsHidden = _downScale.IsHidden = true;
		_upScale.ShowCurrentValue = _downScale.ShowCurrentValue = false;
		_upScale.Color = _downScale.Color = Colors.Transparent;
		_upScale.ScaleIt = _downScale.ScaleIt = true;

		DataSeries[0] = _upScale;
		DataSeries.Add(_downScale);

		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);

		UseAutoSize = true;

		ProportionVolume = 100;
		Width = 200;
		RightToLeft = true;

		BidRows = System.Windows.Media.Color.FromArgb(153, 0, 128, 0);
		TextColor = Colors.White;
		AskRows = System.Windows.Media.Color.FromArgb(153, 255, 0, 0);

		ShowCumulativeValues = true;
		Scale = 20;

		FilterColors.CollectionChanged += FiltersChanged;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_cumulativeAsk = new SortedList<decimal, decimal>();
			_cumulativeBid = new SortedList<decimal, decimal>();
			DataSeries.ForEach(x => x.Clear());

			lock (_locker)
			{
				var depths = MarketDepthInfo.GetMarketDepthSnapshot();
				var mDepth = new SortedList<decimal, MarketDataArg>();

				foreach (var depth in depths)
					mDepth.Add(depth.Price, depth);

				_mDepth = mDepth;

				if (_mDepth.Count == 0)
					return;

				ResetColors();

				_minAsk = _mDepth.FirstOrDefault(x => x.Value.Direction == TradeDirection.Buy).Key;
				_maxBid = _mDepth.LastOrDefault(x => x.Value.Direction == TradeDirection.Sell).Key;

				_maxPrice = _mDepth.Keys.Last();
				_minPrice = _mDepth.Keys.First();

				var maxLevel = _mDepth
					.Values
					.OrderByDescending(x => x.Volume)
					.First();

				_maxVolume = new VolumeInfo
				{
					Price = maxLevel.Price,
					Volume = maxLevel.Volume
				};
			}

			if (VisualMode is not Mode.Common)
			{
				var sum = 0m;

				foreach (var (price, level) in _mDepth.Where(x => x.Value.DataType is MarketDataType.Ask))
				{
					sum += level.Volume;
					_cumulativeAsk[price] = sum;
				}

				sum = 0m;

				foreach (var (price, level) in _mDepth.Where(x => x.Value.DataType is MarketDataType.Bid).OrderByDescending(x => x.Key))
				{
					sum += level.Volume;
					_cumulativeBid[price] = sum;
				}
			}

			return;
		}

		if (UseScale)
		{
			_upScale[CurrentBar - 2] = 0;
			_downScale[CurrentBar - 2] = 0;

			if (_maxPrice != 0)
				_upScale[CurrentBar - 1] = _maxPrice + InstrumentInfo.TickSize * (_scale + 3);

			if (_minPrice != 0)
				_downScale[CurrentBar - 1] = _minPrice - InstrumentInfo.TickSize * (_scale + 3);
		}
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo.PriceChartContainer.TotalBars == -1)
			return;

		if (LastVisibleBarNumber != ChartInfo.PriceChartContainer.TotalBars)
			return;

		if (CurrentBar <= 0)
			return;

		lock (_locker)
		{
			if (_mDepth.Count == 0)
				return;
		}

		var height = (int)Math.Floor(ChartInfo.PriceChartContainer.PriceRowHeight) - 1;

		height = height < 1 ? 1 : height;

		if (PriceLevelsHeight != 0)
			height = PriceLevelsHeight - 2;

		var textAutoSize = GetTextSize(context, height);
		_font = new RenderFont("Arial", textAutoSize);

		var maxVolume = _maxVolume.Volume;

		lock (_locker)
		{
			if (VisualMode is not Mode.Common)
				DrawCumulative(context);

			if (VisualMode is not Mode.Cumulative)
			{
				if (UseAutoSize)
				{
					maxVolume = _mDepth.Values.Max(t => t.Volume);
				}
			}
		}

		if (!UseAutoSize)
			maxVolume = ProportionVolume;

		decimal currentPrice;

		try
		{
			currentPrice = GetCandle(CurrentBar - 1).Close;
		}
		catch (Exception e)
		{
			this.LogDebug("Chart does not contains bars", e);
			return;
		}

		var currentPriceY = ChartInfo.GetYByPrice(currentPrice);

		DrawBackGround(context, currentPriceY);

		lock (_locker)
		{
			var stringRects = new List<(string Text, Rectangle Rect)>();

			if (_mDepth.Values.Any(x => x.DataType is MarketDataType.Ask))
			{
				_asksHistogram = new HistogramRender(!RightToLeft);
				var firstPrice = _minAsk;

				foreach (var priceDepth in _mDepth.Values.Where(x => x.DataType is MarketDataType.Ask))
				{
					int y;

					if (PriceLevelsHeight == 0)
					{
						y = ChartInfo.GetYByPrice(priceDepth.Price);
						height = Math.Abs(y - ChartInfo.GetYByPrice(priceDepth.Price - InstrumentInfo.TickSize)) - 1;

						if (height < 1)
							height = 1;
					}
					else
					{
						height = PriceLevelsHeight - 1;

						if (height < 1)
							height = 1;
						var diff = (priceDepth.Price - firstPrice) / InstrumentInfo.TickSize;
						y = currentPriceY - height * ((int)diff + 1) - (int)diff - 15;
					}

					if (y < ChartInfo.Region.Top)
						continue;

					var width = GetLevelWidth(priceDepth.Volume, maxVolume);

					if (!UseAutoSize)
						width = Math.Min(width, Width);

					if (priceDepth.Price == _minAsk)
					{
						var bestRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, y),
							new Size(Width, height));
						context.FillRectangle(_bestAskBackGround, bestRect);
					}

					var x1 = RightToLeft
						? ChartInfo.Region.Width - width
						: ChartInfo.Region.Width - Width;

					var x2 = x1 + width;
					var botY = y + height;

					var rect = RightToLeft
						? new Rectangle(ChartInfo.Region.Width - width, y, width, height)
						: new Rectangle(new Point(ChartInfo.Region.Width - Width, y), new Size(width, height));

					if (!_filteredColors.TryGetValue(priceDepth.Price, out var fillColor))
						fillColor = _askColor;

					if (_font.Size >= _heightToSolidMode)
					{
						if (_font.Size > 4)
						{
							var renderText = priceDepth.Volume.ToString(_digitFormat);
							var textWidth = context.MeasureString(renderText, _font).Width + 5;

							var textRect = RightToLeft
								? new Rectangle(new Point(ChartInfo.Region.Width - textWidth, y), new Size(textWidth, height))
								: new Rectangle(new Point(ChartInfo.Region.Width - Width, y), new Size(textWidth, height));

							stringRects.Add((renderText, textRect));
						}

						context.FillRectangle(fillColor, rect);
					}
					else
						_asksHistogram.AddPrice(RightToLeft ? x2 : x1, RightToLeft ? x1 : x2, botY, y-1);
				}
			}

			if (_mDepth.Values.Any(x => x.DataType is MarketDataType.Bid))
			{
				_bidsHistogram = new HistogramRender(!RightToLeft);
				var spread = 0;

				if (_mDepth.Values.Any(x => x.DataType is MarketDataType.Ask))
					spread = (int)((_minAsk - _maxBid) / InstrumentInfo.TickSize);

				var firstPrice = _maxBid;

				foreach (var priceDepth in _mDepth.Values.Where(x => x.DataType is MarketDataType.Bid))
				{
					int y;

					if (PriceLevelsHeight == 0)
					{
						y = ChartInfo.GetYByPrice(priceDepth.Price);
						height = Math.Abs(y - ChartInfo.GetYByPrice(priceDepth.Price - InstrumentInfo.TickSize)) - 1;

						if (height < 1)
							height = 1;
					}
					else
					{
						height = PriceLevelsHeight - 1;

						if (height < 1)
							height = 1;
						var diff = (firstPrice - priceDepth.Price) / InstrumentInfo.TickSize;
						y = currentPriceY + height * ((int)diff + spread - 1) + (int)diff - 15;
					}

					if (y > ChartInfo.Region.Bottom)
						continue;

					var width = GetLevelWidth(priceDepth.Volume, maxVolume);

					if (!UseAutoSize)
						width = Math.Min(width, Width);

					if (priceDepth.Price == _maxBid)
					{
						var bestRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, y),
							new Size(Width, height));
						context.FillRectangle(_bestBidBackGround, bestRect);
					}

					var x1 = RightToLeft
						? ChartInfo.Region.Width - width
						: ChartInfo.Region.Width - Width;

					var x2 = x1 + width;
					var botY = y + height;

					var rect = RightToLeft
						? new Rectangle(ChartInfo.Region.Width - width, y, width, height)
						: new Rectangle(new Point(ChartInfo.Region.Width - Width, y), new Size(width, height));

					if (!_filteredColors.TryGetValue(priceDepth.Price, out var fillColor))
						fillColor = _bidColor;

					if (_font.Size >= _heightToSolidMode)
					{
						if (_font.Size > 4)
						{
							var renderText = priceDepth.Volume.ToString(_digitFormat);
							var textWidth = context.MeasureString(renderText, _font).Width;

							var textRect = RightToLeft
								? new Rectangle(new Point(ChartInfo.Region.Width - textWidth, y), new Size(textWidth, height))
								: new Rectangle(new Point(ChartInfo.Region.Width - Width, y), new Size(textWidth, height));

							stringRects.Add((renderText, textRect));
						}

						context.FillRectangle(fillColor, rect);
					}
					else
						_bidsHistogram.AddPrice(RightToLeft ? x2 : x1, RightToLeft ? x1 : x2, botY, y-1);
				}
			}

			if (_font.Size < _heightToSolidMode)
			{
				_asksHistogram.Draw(context, _askColor, true);
				_bidsHistogram.Draw(context, _bidColor, true);
			}

			foreach (var (text, rect) in stringRects)
			{
				context.DrawString(text,
					_font,
					_textColor,
					rect,
					RightToLeft ? _stringRightFormat : _stringLeftFormat);
			}

			if (ShowCumulativeValues)
				DrawCumulativeValues(context);
        }
	}

	protected override void OnBestBidAskChanged(MarketDataArg depth)
	{
		if (depth.DataType is MarketDataType.Ask)
			_minAsk = depth.Price;
		else
			_maxBid = depth.Price;

		RedrawChart(_emptyRedrawArg);
	}

	protected override void MarketDepthChanged(MarketDataArg depth)
	{
		lock (_locker)
		{
			var isCumulative = VisualMode is not Mode.Common;
			_mDepth.Remove(depth.Price);
			_filteredColors.Remove(depth.Price);

			if (isCumulative)
			{
				if (depth.DataType is MarketDataType.Bid)
					_cumulativeBid.Remove(depth.Price);
				else
					_cumulativeAsk.Remove(depth.Price);
			}

			if (depth.Volume != 0)
			{
				_mDepth.Add(depth.Price, depth);
				var passedFilters = FilterColors.Where(x => x.Value <= depth.Volume).ToList();

				if (passedFilters.Any())
				{
					var filterColor = passedFilters
						.OrderByDescending(x => x.Value)
						.First()
						.Color;

					_filteredColors.Add(depth.Price, filterColor);
				}
			}

			if (_mDepth.Count == 0)
			{
				if (isCumulative)
				{
					_cumulativeAsk = new SortedList<decimal, decimal>();
					_cumulativeBid = new SortedList<decimal, decimal>();
				}

				return;
			}

			if (UseScale || isCumulative)
			{
				if (depth.Price >= _maxPrice || depth.Volume == 0)
				{
					if (depth.Price >= _maxPrice && depth.Volume != 0)
						_maxPrice = depth.Price;
					else if (depth.Price >= _maxPrice && depth.Volume == 0)
						_maxPrice = _mDepth.Keys.LastOrDefault();

					if (UseScale)
						_upScale[CurrentBar - 1] = _maxPrice + InstrumentInfo.TickSize * (_scale + 3);
				}

				if (depth.Price <= _minPrice || depth.Volume == 0)
				{
					if (depth.Price <= _minPrice && depth.Volume != 0)
						_minPrice = depth.Price;
					else if (depth.Price <= _minPrice && depth.Volume == 0)
						_minPrice = _mDepth.Keys.FirstOrDefault();

					if (UseScale)
						_downScale[CurrentBar - 1] = _minPrice - InstrumentInfo.TickSize * (_scale + 3);
				}
			}

			if (depth.Price == _maxVolume.Price)
			{
				if (depth.Volume >= _maxVolume.Volume)
					_maxVolume.Volume = depth.Volume;
				else
				{
					var priceLevel = _mDepth.Values
						.OrderByDescending(x => x.Volume)
						.First();

					_maxVolume.Price = priceLevel.Price;
					_maxVolume.Volume = priceLevel.Volume;
				}
			}
			else
			{
				if (depth.Volume > _maxVolume.Volume)
				{
					_maxVolume.Price = depth.Price;
					_maxVolume.Volume = depth.Volume;
				}
			}

			if (isCumulative)
			{
				if (depth.DataType is MarketDataType.Ask)
				{
					var sum = _cumulativeAsk.LastOrDefault(x => x.Key < depth.Price).Value;

					foreach (var (price, level) in _mDepth.Where(x => x.Key >= depth.Price && x.Value.DataType is MarketDataType.Ask))
					{
						sum += level.Volume;
						_cumulativeAsk[price] = sum;
					}
				}
				else
				{
					var sum = _cumulativeBid.FirstOrDefault(x => x.Key > depth.Price).Value;

					var otherBids = _mDepth
						.Where(x => x.Key <= depth.Price && x.Value.DataType is MarketDataType.Bid)
						.ToArray();

					for (var i = otherBids.Length - 1; i >= 0; i--)
					{
						var level = otherBids[i];

						sum += level.Value.Volume;
						_cumulativeBid[level.Key] = sum;
					}
				}
			}
		}

		RedrawChart(_emptyRedrawArg);
	}

	#endregion

	#region Private methods

	private void DrawCumulativeValues(RenderContext context)
	{
		var maxWidth = (int)Math.Round(ChartInfo.Region.Width * 0.2m);
		var totalVolume = MarketDepthInfo.CumulativeDomAsks + MarketDepthInfo.CumulativeDomBids;

		if (totalVolume == 0)
			return;

		var font = new RenderFont("Arial", 9);

		var askRowWidth = (int)Math.Round(MarketDepthInfo.CumulativeDomAsks * (maxWidth - 1) / totalVolume);
		var bidRowWidth = maxWidth - askRowWidth;
		var yRect = ChartInfo.Region.Bottom - _unitedVolumeHeight;
		var bidStr = $"{MarketDepthInfo.CumulativeDomBids:0.##}";
		var askStr = $"{MarketDepthInfo.CumulativeDomAsks:0.##}";

		var askWidth = context.MeasureString(askStr, font).Width;
		var bidWidth = context.MeasureString(bidStr, font).Width;

		if (askWidth > askRowWidth && MarketDepthInfo.CumulativeDomAsks != 0)
		{
			askRowWidth = askWidth;
			maxWidth = (int)Math.Round(Math.Min(ChartInfo.Region.Width * 0.3m, totalVolume * askRowWidth / MarketDepthInfo.CumulativeDomAsks + 1));
			bidRowWidth = maxWidth - askRowWidth;
		}

		if (bidWidth > bidRowWidth && MarketDepthInfo.CumulativeDomBids != 0)
		{
			bidRowWidth = bidWidth;
			maxWidth = (int)Math.Round(Math.Min(ChartInfo.Region.Width * 0.3m, totalVolume * bidRowWidth / MarketDepthInfo.CumulativeDomBids + 1));
			askRowWidth = maxWidth - bidRowWidth;
		}

		if (askRowWidth > 0)
		{
			var askRect = new Rectangle(new Point(ChartInfo.Region.Width - askRowWidth, yRect),
				new Size(askRowWidth, _unitedVolumeHeight));
			context.FillRectangle(_volumeAskColor, askRect);
			context.DrawString(askStr, font, _bidColor, askRect, _stringLeftFormat);
		}

		if (bidRowWidth > 0)
		{
			var bidRect = new Rectangle(new Point(ChartInfo.Region.Width - maxWidth, yRect),
				new Size(bidRowWidth, _unitedVolumeHeight));
			context.FillRectangle(_volumeBidColor, bidRect);
			context.DrawString(bidStr, font, _askColor, bidRect, _stringRightFormat);
		}
	}

	private void DrawCumulative(RenderContext context)
	{
		_cumulativeHistogram = new MultiColorsHistogramRender(CumulativeAskColor, !RightToLeft);

		var maxVolume = Math.Max(
			_cumulativeAsk.Values.DefaultIfEmpty(0).Max(),
			_cumulativeBid.Values.DefaultIfEmpty(0).Max());

		var startX = RightToLeft ? Container.Region.Width : Container.Region.Width - Width;

		if (_mDepth.Any(x => x.Value.DataType is MarketDataType.Ask))
		{
			var curIdx = 0;
			var lastIdx = _cumulativeAsk.Count - 1;

			foreach (var (price, volume) in _cumulativeAsk)
			{
				var levelWidth = (int)(Width * volume / maxVolume);

				var x = RightToLeft
					? Container.Region.Width - levelWidth
					: Container.Region.Width - Width + levelWidth;

				var y1 = ChartInfo.GetYByPrice(price - InstrumentInfo.TickSize);

				var y2 = curIdx == lastIdx
					? ChartInfo.GetYByPrice(price)
					: ChartInfo.GetYByPrice(_cumulativeAsk.Keys[curIdx + 1] - InstrumentInfo.TickSize);

				_cumulativeHistogram.AddPrice(startX, x, y1, y2, CumulativeAskColor);
				curIdx++;
			}
		}

		if (_mDepth.Any(x => x.Value.DataType is MarketDataType.Bid))
		{
			var curIdx = 0;
			var lastIdx = _cumulativeBid.Count - 1;

			foreach (var (price, volume) in _cumulativeBid)
			{
				var levelWidth = (int)(Width * volume / maxVolume);

				var x = RightToLeft
					? Container.Region.Width - levelWidth
					: Container.Region.Width - Width + levelWidth;

				var y1 = ChartInfo.GetYByPrice(price - InstrumentInfo.TickSize);

				var y2 = curIdx == lastIdx
					? ChartInfo.GetYByPrice(price)
					: ChartInfo.GetYByPrice(_cumulativeBid.Keys[curIdx + 1]);

				_cumulativeHistogram.AddPrice(startX, x, y1, y2, CumulativeBidColor);
				curIdx++;
			}
		}

		_cumulativeHistogram.Draw(context, true);

		if (VisualMode is Mode.Cumulative)
		{
			foreach (var (price, volume) in _cumulativeBid)
				DrawText(context, price, volume);

			foreach (var (price, volume) in _cumulativeAsk)
				DrawText(context, price, volume);
		}
	}

	private void DrawText(RenderContext context, decimal price, decimal volume)
	{
		var form = RightToLeft ? _stringRightFormat : _stringLeftFormat;

		var y = ChartInfo.GetYByPrice(price);
		var renderText = volume.ToString(_digitFormat);
		var textWidth = context.MeasureString(renderText, _font).Width + 5;

		var textRect = new Rectangle(new Point(ChartInfo.Region.Width - textWidth, y),
			new Size(textWidth, (int)ChartInfo.PriceChartContainer.PriceRowHeight));

		if (!RightToLeft)
		{
			textRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, y),
				new Size(textWidth, (int)ChartInfo.PriceChartContainer.PriceRowHeight));
		}

		if (_font.Size >= 6)
		{
			context.DrawString(renderText,
				_font,
				_textColor,
				textRect,
				form);
		}
	}

	private int GetLevelWidth(decimal curVolume, decimal maxVolume)
	{
		var width = Math.Floor(curVolume * Width /
			(maxVolume == 0 ? 1 : maxVolume));

		return (int)Math.Min(Container.Region.Width, width);
	}

	private void DrawBackGround(RenderContext context, int priceY)
	{
		if (PriceLevelsHeight == 0)
		{
			var y2 = ChartInfo.GetYByPrice(_minAsk - InstrumentInfo.TickSize);
			var y3 = ChartInfo.GetYByPrice(_maxBid);
			var y4 = ChartInfo.Region.Height;

			var fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, 0), new Size(Width, y2));

			context.FillRectangle(_askBackGround, fullRect);

			fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, y3),
				new Size(Width, y4 - y3));

			context.FillRectangle(_bidBackGround, fullRect);
		}
		else
		{
			var spread = (int)((_minAsk - _maxBid) / InstrumentInfo.TickSize);
			var y = priceY - 15;

			var fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, 0), new Size(Width, y));
			context.FillRectangle(_askBackGround, fullRect);

			y = priceY + (PriceLevelsHeight - 1) * (spread - 1) - 15;
			fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, y), new Size(Width, ChartInfo.Region.Height - y));
			context.FillRectangle(_bidBackGround, fullRect);
		}
	}

	private void FiltersChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems != null)
		{
			foreach (var item in e.NewItems)
				((INotifyPropertyChanged)item).PropertyChanged += ItemPropertyChanged;
		}

		if (e.OldItems != null)
		{
			foreach (var item in e.OldItems)
				((INotifyPropertyChanged)item).PropertyChanged -= ItemPropertyChanged;
		}

		ResetColors();
	}

	private void ItemPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		ResetColors();
	}

	private void ResetColors()
	{
		_filteredColors.Clear();

		foreach (var arg in _mDepth.Values)
		{
			var passedFilters = FilterColors
				.Where(x => x.Value <= arg.Volume)
				.ToList();

			if (!passedFilters.Any())
				continue;

			var filterColor = passedFilters
				.OrderByDescending(x => x.Value)
				.First()
				.Color;

			_filteredColors.Add(arg.Price, filterColor);
		}
	}

	private int GetTextSize(RenderContext context, int height)
	{
		for (var i = _fontSize; i > 0; i--)
		{
			if (context.MeasureString("12", new RenderFont("Arial", i)).Height < height + 5)
				return i;
		}

		return 0;
	}

	#endregion
}

public class FilterColor : INotifyPropertyChanged
{
	#region Fields

	private Color _color = Color.LightBlue;
	private decimal _value;

	#endregion

	#region Properties

	public decimal Value
	{
		get => _value;
		set
		{
			_value = value;
			OnPropertyChanged();
		}
	}

	public Color Color
	{
		get => _color;
		set
		{
			_color = value;
			OnPropertyChanged();
		}
	}

	#endregion

	#region Events

	public event PropertyChangedEventHandler PropertyChanged;

	#endregion

	#region Protected methods

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	#endregion
}