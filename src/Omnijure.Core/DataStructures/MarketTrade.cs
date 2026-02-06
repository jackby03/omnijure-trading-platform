
using System;

namespace Omnijure.Core.DataStructures;

public struct MarketTrade
{
    public float Price;
    public float Quantity;
    public long Timestamp;
    public bool IsBuyerMaker; // True means Sell, False means Buy on Binance
}
