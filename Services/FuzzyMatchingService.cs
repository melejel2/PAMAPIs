using System;
using System.Collections.Generic;
using FuzzySharp;
using PAMAPIs.Data;

namespace PAMAPIs.Services
{
    public class FuzzyMatchingService
    {
        private readonly PAMContext _dbContext;

        public FuzzyMatchingService(PAMContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public List<string> GetClosestMatches(string userInput, int numberOfMatches = 7)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                throw new ArgumentException("User input cannot be null or empty.", nameof(userInput));
            }

            List<string> items;
            try
            {
                items = _dbContext.Items.Select(i => i.ItemName).ToList();
            }
            catch (Exception ex)
            {
                // Handle potential database exceptions here
                throw new InvalidOperationException("Error retrieving items from the database.", ex);
            }

            var results = Process.ExtractTop(userInput, items, limit: numberOfMatches);
            return results.Select(r => r.Value).ToList();
        }
    }
}