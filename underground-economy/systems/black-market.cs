using System;
using System.Collections.Generic;

namespace LogisticsTycoon
{
    public class BlackMarketSystem
    {
        private readonly Random _random = new Random();
        private readonly List<SmugglingRoute> _smugglingRoutes = new List<SmugglingRoute>();
        private readonly List<IllicitGood> _illicitGoods = new List<IllicitGood>();
        
        public BlackMarketSystem()
        {
            // Initialize smuggling routes
            _smugglingRoutes.Add(new SmugglingRoute("Portugal-Belgium", 0.75f, 0.25f));
            _smugglingRoutes.Add(new SmugglingRoute("Spain-France", 0.65f, 0.35f));
            _smugglingRoutes.Add(new SmugglingRoute("Netherlands-Germany", 0.80f, 0.20f));
            
            // Initialize illicit goods
            _illicitGoods.Add(new IllicitGood("Counterfeit Electronics", 5000, 0.15f));
            _illicitGoods.Add(new IllicitGood("Stolen Luxury Goods", 10000, 0.25f));
            _illicitGoods.Add(new IllicitGood("Illicit Drugs", 20000, 0.35f));
        }
        
        public void HandleSmugglingOpportunity(Vehicle vehicle, Driver driver)
        {
            // Check if driver has smuggling倾向
            if (_random.NextDouble() < driver.SmugglingTendency)
            {
                // Find suitable smuggling route
                var route = GetSuitableSmugglingRoute(vehicle);
                if (route != null)
                {
                    // Calculate potential profit
                    var potentialProfit = CalculateSmugglingProfit(route, vehicle);
                    
                    // Check if vehicle can carry the goods
                    if (vehicle.LoadCapacity > vehicle.CurrentLoad)
                    {
                        // Execute smuggling operation
                        ExecuteSmugglingOperation(vehicle, driver, route, potentialProfit);
                    }
                    else
                    {
                        Console.WriteLine("Vehicle is already fully loaded. Cannot execute smuggling operation.");
                    }
                }
                else
                {
                    Console.WriteLine("No suitable smuggling route found for this vehicle.");
                }
            }
        }
        
        private SmugglingRoute GetSuitableSmugglingRoute(Vehicle vehicle)
        {
            foreach (var route in _smugglingRoutes)
            {
                if (vehicle.Type == "Van" && route.CountryPair.Contains("Portugal-Belgium"))
                {
                    return route;
                }
                else if (vehicle.Type == "Rigid Truck" && route.CountryPair.Contains("Spain-France"))
                {
                    return route;
                }
                else if (vehicle.Type == "Semi-Truck" && route.CountryPair.Contains("Netherlands-Germany"))
                {
                    return route;
                }
            }
            return null;
        }
        
        private float CalculateSmugglingProfit(SmugglingRoute route, Vehicle vehicle)
        {
            var baseProfit = 0.0f;
            
            // Calculate base profit based on route risk and goods value
            foreach (var good in _illicitGoods)
            {
                if (vehicle.LoadCapacity > vehicle.CurrentLoad)
                {
                    baseProfit += good.Value * route.RiskFactor;
                }
            }
            
            // Calculate potential profit considering vehicle condition
            return baseProfit * (1.0f - vehicle.EngineCondition / 100.0f);
        }
        
        private void ExecuteSmugglingOperation(Vehicle vehicle, Driver driver, SmugglingRoute route, float potentialProfit)
        {
            // Calculate actual profit based on risk and vehicle condition
            var actualProfit = potentialProfit * _random.Next(70, 101) / 100.0f;
            
            // Apply risk factors
            var riskImpact = _random.Next(10, 31) / 100.0f;
            var actualProfitAfterRisk = actualProfit * (1.0f - riskImpact);
            
            // Update vehicle and driver states
            vehicle.CurrentLoad += 1000; // Add 1000 kg of contraband
            driver.SmugglingExperience += 10;
            
            // Add to player's black market income
            Player.Instance.BlackMarketIncome += actualProfitAfterRisk;
            
            Console.WriteLine("Smuggling operation successful! Earned: "+actualProfitAfterRisk.ToString("C2")+
                " from route " + route.CountryPair +
                " with risk impact of " + riskImpact.ToString("P2")+
                " and vehicle condition factor of " + (1.0f - vehicle.EngineCondition / 100.0f).ToString("P2")+
                ".");
        }
    }
    
    public class SmugglingRoute
    {
        public string CountryPair { get; }
        public float RiskFactor { get; }
        public float RevenueMultiplier { get; }
        
        public SmugglingRoute(string countryPair, float riskFactor, float revenueMultiplier)
        {
            CountryPair = countryPair;
            RiskFactor = riskFactor;
            RevenueMultiplier = revenueMultiplier;
        }
    }
    
    public class IllicitGood
    {
        public string Name { get; }
        public float Value { get; }
        public float RiskFactor { get; }
        
        public IllicitGood(string name, float value, float riskFactor)
        {
            Name = name;
            Value = value;
            RiskFactor = riskFactor;
        }
    }
}