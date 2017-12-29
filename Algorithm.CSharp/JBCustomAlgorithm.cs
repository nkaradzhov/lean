using System;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System.Collections.Generic;
using QuantConnect.Data.Consolidators;

namespace QuantConnect.Algorithm.CSharp
{
    public class JBCustomAlgorithm : QCAlgorithm
    {
        private readonly string _ticker = "LTCUSD";
        private readonly Resolution RESOLUTION = Resolution.Minute;
        private readonly int EVERY = 1;

        private readonly int MACD_FAST = 12;
        private readonly int MACD_SLOW = 26;
        private readonly int MACD_SIGNAL = 9;

        private readonly decimal SECURE_PROFIT_PERCENT = 1.15m;
        private readonly decimal STOP_LOSS_PERCENT = 0.98m;

        private MovingAverageConvergenceDivergence _macd;
        private List<Orders.OrderTicket> _currentOpenOrders = new List<Orders.OrderTicket>();

        public override void Initialize()
        {
            SetStartDate(2017, 12, 01);
            SetEndDate(2017, 12, 08);
            SetCash(100000);
            AddCrypto(_ticker, RESOLUTION, Market.GDAX);

            var consolidator = new TradeBarConsolidator(EVERY);
            consolidator.DataConsolidated += BarHandler;

            _macd = new MovingAverageConvergenceDivergence(MACD_FAST, MACD_SLOW, MACD_SIGNAL, MovingAverageType.Exponential);
            RegisterIndicator(Symbol(_ticker), _macd, consolidator, (arg) => arg.Value);

            SubscriptionManager.AddConsolidator(Symbol(_ticker), consolidator);

        }

        private void BarHandler(object sender, TradeBar data)
        {
            if (!_macd.IsReady) return;

            var holding = Portfolio[_ticker];

            var signalDeltaPercent = (_macd - _macd.Signal) / _macd.Fast;
            var tolerance = 0.0025m;

            // if our macd is greater than our signal, then let's go long
            if (holding.Quantity <= 0 && signalDeltaPercent > tolerance) // 0.01%
            {
                ClearPendingOrders();
                Debug("Buy at " + data.Price);
                SetHoldings(Symbol(_ticker), 1.0);
            }
            // of our macd is less than our signal, then let's go short
            else if (holding.Quantity > 0 && signalDeltaPercent < -tolerance)
            {
                ClearPendingOrders();
                Debug("Sell at " + data.Price);
                Liquidate(Symbol(_ticker));
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
    }

}




