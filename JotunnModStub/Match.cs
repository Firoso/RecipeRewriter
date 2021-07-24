using Newtonsoft.Json.Linq;
using System;

namespace RecipeRewriter
{
    public class Match
    {
        public Match(string name, int? amount, string buildTool)
        {
            this.Name = name;
            this.Amount = amount;
            this.BuildTool = buildTool;
        }

        public string Name { get; set; }
        public int? Amount { get; set; }
        public string BuildTool { get; set; }

        public static Match FromJsonObjectWithMatchProperty(JObject piece)
        {
            // match a piece
            var matchProperty = piece.Property("match", StringComparison.InvariantCultureIgnoreCase).Value.Value<JObject>();

            var nameProperty = matchProperty.Property("name", StringComparison.InvariantCultureIgnoreCase);
            if (nameProperty != null)
            {
                RecipeRewriter.Log.LogMessage($"Found Match Criteria 'name' of '{nameProperty.Value.Value<string>()}'");
            }

            var amountProperty = matchProperty.Property("amount", StringComparison.InvariantCultureIgnoreCase);
            if (amountProperty != null)
            {
                RecipeRewriter.Log.LogMessage($"Found Match Criteria 'amount' of '{amountProperty.Value.Value<int>()}'");
            }

            var buildToolProperty = matchProperty.Property("buildTool", StringComparison.InvariantCultureIgnoreCase);
            if (buildToolProperty != null)
            {
                RecipeRewriter.Log.LogMessage($"Found Match Criteria 'buildTool' of '{buildToolProperty.Value.Value<string>()}'");
            }

            return new Match(nameProperty.Value.Value<string>(), amountProperty?.Value?.Value<int>(), buildToolProperty?.Value.Value<string>() ?? null);
        }
    }
}
