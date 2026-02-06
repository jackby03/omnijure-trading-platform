
using System;
using System.Collections.Generic;

namespace Omnijure.Core.DataStructures;

public struct OrderBookEntry
{
    public float Price;
    public float Quantity;
}

public class OrderBook
{
    public string Symbol { get; set; } = string.Empty;
    public List<OrderBookEntry> Bids { get; set; } = new();
    public List<OrderBookEntry> Asks { get; set; } = new();
    
    public void Update(List<OrderBookEntry> bids, List<OrderBookEntry> asks)
    {
        lock (this)
        {
            Bids = bids;
            Asks = asks;
        }
    }
}
