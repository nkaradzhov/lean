using System;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System.Collections.Generic;
using QuantConnect.Data.Consolidators;

namespace QuantConnect.Algorithm.CSharp
{
    public enum Mode
    {
        MACD, HISTOGRAM, RSI
    }

    public class JBCustomAlgorithm : QCAlgorithm
    {
        private readonly string _ticker = "LTCUSD";
        private readonly int EVERY = 1;
        private readonly Resolution RESOLUTION = Resolution.Minute;

        /*** Mode.MACD Mode.HISTOGRAM Mode.RSI ***/
        private readonly Mode mode = Mode.RSI;

        /*** MACD params ***/
        private readonly int MACD_FAST = 12;
        private readonly int MACD_SLOW = 26;
        private readonly int MACD_SIGNAL = 9;
        /*** MACD params ***/

        /*** Histogram params ***/
        private readonly decimal STEP = 0.04m; // 0.04m za histogram1 0.02 za histogram2
        /*** Histogram params ***/

        /*** RSI params ***/
        private readonly int PERIOD = 7;
        private readonly int HIGH = 70;
        private readonly int LOW = 30;
        /*** RSI params ***/

        /*** set 0 to turn off ***/
        private readonly decimal SECURE_PROFIT_PERCENT = 1.15m;
        /*** set 0 to turn off ***/
        private readonly decimal STOP_LOSS_PERCENT = 0.98m;


        private MovingAverageConvergenceDivergence _macd;
        private ParabolicStopAndReverse _sar;
        private RelativeStrengthIndex _rsi;

        private List<Orders.OrderTicket> _currentOpenOrders = new List<Orders.OrderTicket>();
        private TradeBarConsolidator consolidator;

        public override void Initialize()
        {
            SetStartDate(2017, 12, 01);
            SetEndDate(2017, 12, 08);
            SetCash(100000);
            AddCrypto(_ticker, RESOLUTION, Market.GDAX);

            consolidator = new TradeBarConsolidator(EVERY);
            consolidator.DataConsolidated += BarHandler;

            InitIndicators();

            SubscriptionManager.AddConsolidator(Symbol(_ticker), consolidator);

        }

        private void InitIndicators()
        {
            if (mode == Mode.MACD)
            {
                _macd = new MovingAverageConvergenceDivergence(MACD_FAST, MACD_SLOW, MACD_SIGNAL, MovingAverageType.Exponential);
                RegisterIndicator(Symbol(_ticker), _macd, consolidator);
                return;
            }
            if (mode == Mode.HISTOGRAM)
            {
                _sar = new ParabolicStopAndReverse("SAR", afIncrement: STEP);
                RegisterIndicator(Symbol(_ticker), _sar, consolidator);
                return;
            }
            if (mode == Mode.RSI)
            {
                _rsi = new RelativeStrengthIndex(PERIOD, MovingAverageType.Exponential);
                RegisterIndicator(Symbol(_ticker), _rsi, consolidator);
                return;
            }
            Debug("Invalid mode: " + mode);
        }

        private void BarHandler(object sender, TradeBar data)
        {
            if (mode == Mode.MACD)
            {
                HandleMACD(sender, data);
                return;
            }
            if (mode == Mode.HISTOGRAM)
            {
                HandleHistogram(sender, data);
                return;
            }
            if (mode == Mode.RSI)
            {
                HandleRSI(sender, data);
                return;
            }
            Debug("Invalid mode: " + mode);
        }

        private void HandleRSI(object sneder, TradeBar data)
        {
            if (!_rsi.IsReady) return;

            var holding = Portfolio[_ticker];

            if (holding.Quantity <= 0 && _rsi < LOW)
            {
                Buy(data);
            }
            else if (holding.Quantity > 0 && _rsi > HIGH)
            {
                Sell(data);
            }
        }

        private void HandleHistogram(object sender, TradeBar data)
        {
            if (!_sar.IsReady) return;

            //Debug(data.Price + " " + data.Close + " " + _sar);

            var holding = Portfolio[_ticker];

            if (holding.Quantity <= 0 && _sar < data.Price)
            {
                Buy(data);
            }
            else if (holding.Quantity > 0 && _sar > data.Price)
            {
                Sell(data);
            }
        }

        private void HandleMACD(object sender, TradeBar data)
        {
            if (!_macd.IsReady) return;

            var holding = Portfolio[_ticker];

            var signalDeltaPercent = (_macd - _macd.Signal) / _macd.Fast;
            var tolerance = 0.0025m;

            // if our macd is greater than our signal, then let's go long
            if (holding.Quantity <= 0 && signalDeltaPercent > tolerance) // 0.01%
            {
                Buy(data);
            }
            // of our macd is less than our signal, then let's go short
            else if (holding.Quantity > 0 && signalDeltaPercent < -tolerance)
            {
                Sell(data);
            }

            // plot both lines
            Plot("MACD", _macd, _macd.Signal);
            Plot(_ticker, "Open", data.Open);
            Plot(_ticker, _macd.Fast, _macd.Slow);
        }

        public void OnData(TradeBars data)
        {

        }

        public override void OnOrderEvent(Orders.OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            if (orderEvent.Status == Orders.OrderStatus.Filled)
            {
                Debug(orderEvent.ToString());
                if (orderEvent.Direction == Orders.OrderDirection.Buy)
                {
                    var currentQuantity = orderEvent.FillQuantity;

                    if (SECURE_PROFIT_PERCENT > 0)
                    {
                        var price = orderEvent.FillPrice * SECURE_PROFIT_PERCENT;
                        Debug("Secure profit for " + price);
                        _currentOpenOrders.Add(
                            StopLimitOrder(Symbol(_ticker), -currentQuantity, price, price)
                        );
                    }
                    if (STOP_LOSS_PERCENT > 0)
                    {
                        var price = orderEvent.FillPrice * STOP_LOSS_PERCENT;
                        Debug("Stop los for " + price);
                        _currentOpenOrders.Add(
                            StopLimitOrder(Symbol(_ticker), -currentQuantity, price, price)
                        );
                    }
                }
                else
                {
                    ClearPendingOrders();
                }
            }
        }

        private void ClearPendingOrders()
        {
            foreach (var openOrder in _currentOpenOrders)
            {
                if (openOrder.Status == Orders.OrderStatus.New
                   || openOrder.Status == Orders.OrderStatus.Submitted)
                {
                    openOrder.Cancel();
                }
            }
            _currentOpenOrders.Clear();
        }

        private void Buy(TradeBar data)
        {
            ClearPendingOrders();
            Debug("Buy at " + data.Price);
            SetHoldings(Symbol(_ticker), 1.0);
        }

        private void Sell(TradeBar data)
        {
            ClearPendingOrders();
            Debug("Sell at " + data.Price);
            Liquidate(Symbol(_ticker));
        }
    }

}




