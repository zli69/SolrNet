﻿#region license
// Copyright (c) 2007-2009 Mauricio Scheffer
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
//  
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using SampleSolrApp.Models;
using SolrNet;
using SolrNet.Commands.Parameters;
using SolrNet.DSL;

namespace SampleSolrApp.Controllers {
    [HandleError]
    public class HomeController : Controller {
        private readonly ISolrReadOnlyOperations<Product> solr;

        public HomeController(ISolrReadOnlyOperations<Product> solr) {
            this.solr = solr;
        }

        /// <summary>
        /// Builds the Solr query from the search parameters
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public ISolrQuery BuildQuery(SearchParameters parameters) {
            var queriesFromFacets = from p in parameters.Facets
                                    select (ISolrQuery) Query.Field(p.Key).Is(p.Value);
            var queries = queriesFromFacets.ToList();
            if (!string.IsNullOrEmpty(parameters.FreeSearch))
                queries.Add(new SolrQuery(parameters.FreeSearch));
            if (queries.Count == 0)
                return SolrQuery.All;
            return new SolrMultipleCriteriaQuery(queries, SolrMultipleCriteriaQuery.Operator.AND);
        }

        /// <summary>
        /// All selectable facet fields
        /// </summary>
        private static readonly string[] AllFacetFields = new[] {"cat", "manu_exact"};

        /// <summary>
        /// Gets the selected facet fields
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public IEnumerable<string> SelectedFacetFields(SearchParameters parameters) {
            return parameters.Facets.Select(f => f.Key);
        }

        public SortOrder[] GetSelectedSort(SearchParameters parameters) {
            return new[] {SortOrder.Parse(parameters.Sort)}.Where(o => o != null).ToArray();
        }

        public ActionResult Index(SearchParameters parameters) {
            var start = (parameters.PageIndex - 1)*parameters.PageSize;
            var matchingProducts = solr.Query(BuildQuery(parameters), new QueryOptions {
                Rows = parameters.PageSize,
                Start = start,
                OrderBy = GetSelectedSort(parameters),
                SpellCheck = new SpellCheckingParameters(),
                Facet = new FacetParameters {
                    Queries = AllFacetFields.Except(SelectedFacetFields(parameters))
                                                                          .Select(f => new SolrFacetFieldQuery(f) {MinCount = 1})
                                                                          .Cast<ISolrFacetQuery>()
                                                                          .ToList(),
                },
            });
            var view = new ProductView {
                Products = matchingProducts,
                Search = parameters,
                TotalCount = matchingProducts.NumFound,
                Facets = matchingProducts.FacetFields,
                DidYouMean = GetSpellCheckingResult(matchingProducts),
            };
            return View(view);
        }

        private string GetSpellCheckingResult(ISolrQueryResults<Product> products) {
            return string.Join(" ", products.SpellChecking
                                        .Select(c => c.Suggestions.FirstOrDefault())
                                        .Where(c => !string.IsNullOrEmpty(c))
                                        .ToArray());
        }
    }
}