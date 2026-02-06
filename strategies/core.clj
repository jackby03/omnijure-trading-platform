(ns strategies.core)

;; WHALE KILLER STRATEGY
;; Rules:
;; 1. Buy if RSI < 30 (Oversold) AND RVOL > 1.5 (Whale Activity)
;; 2. Sell if RSI > 70 (Overbought)
;; 3. Hold otherwise

(defn on-tick [signals]
  ;; Extract values from the C# Dictionary
  (let [price (get signals "price")
        rsi   (get signals "rsi")
        rvol  (get signals "rvol")]
    
    ;(println (str "Price: " price " RSI: " rsi " RVOL: " rvol))
    
    (cond
      (and (< rsi 40) (> rvol 0.8)) :buy   ;; Relaxed rules for random data test
      (> rsi 60)                    :sell
      :else                         :hold)))
