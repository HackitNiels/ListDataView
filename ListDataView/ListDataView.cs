using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Linq;

namespace ListDataView
{
    /// <summary>
    /// Represents a databindable, customized view of a <see cref="List<T>"/> for sorting, filtering, searching, editing, and navigation. 
    /// The <see cref="ListDataView"/> does not store data, but instead represents a connected view of its corresponding <see cref="List<T>"/>. Changes to the <see cref="ListDataView"/>'s data will affect the <see cref="List<T>"/>. Changes to the <see cref="List<T>"/>’s data will affect all <see cref="ListDataView"/>s associated with it.
    /// </summary>
    /// <typeparam name="T">Represents the object type in the corresponding <see cref="List<T>"/></typeparam>
    public class ListDataView<T> : BindingList<T>, IBindingListView
    {

        public ListDataView()
        { }

        private List<T> originalListValue = new List<T>();
        // <summary>
        /// Gets or sets the source <see cref="List<T>"/>.
        /// </summary>
        /// <returns>A <see cref="List<T>"/> that provides the data for this view.</returns>
        public List<T> List
        {
            get
            { return originalListValue; }
            set { originalListValue = value; }
        }
        #region Searching

        protected override bool SupportsSearchingCore
        {
            get
            {
                return true;
            }
        }

        protected override int FindCore(PropertyDescriptor prop, object key)
        {
            // Get the property info for the specified property.
            PropertyInfo propInfo = typeof(T).GetProperty(prop.Name);
            T item;

            if (key != null)
            {
                // Loop through the items to see if the key
                // value matches the property value.
                for (int i = 0; i < Count; ++i)
                {
                    item = (T)Items[i];
                    if (propInfo.GetValue(item, null).Equals(key))
                        return i;
                }
            }
            return -1;
        }
        /// <summary>
        /// Finds an object in the <see cref="ListDataView"/> by the specified property name and sort key value.
        /// </summary>
        /// <param name="property">The property to search for the Key</param>
        /// <param name="key">The value to find</param>
        /// <returns>The index of the object in the <see cref="ListDataView"/> that contains the sort key value specified; 
        /// Otherwise returns -1 if the sort key value does not exist.</returns> 
        public int Find(string property, object key)
        {
            // Check the properties for a property with the specified name.
            PropertyDescriptorCollection properties =
                TypeDescriptor.GetProperties(typeof(T));
            PropertyDescriptor prop = properties.Find(property, true);

            // If there is not a match, return -1 otherwise pass search to
            // FindCore method.
            if (prop == null)
                return -1;
            else
                return FindCore(prop, key);
        }

        #endregion Searching

        #region Sorting
        ArrayList sortedList;
        ListDataView<T> unsortedItems;
        bool isSortedValue;
        ListSortDirection sortDirectionValue;
        PropertyDescriptor sortPropertyValue;

        protected override bool SupportsSortingCore
        {
            get { return true; }
        }

        protected override bool IsSortedCore
        {
            get { return isSortedValue; }
        }

        protected override PropertyDescriptor SortPropertyCore
        {
            get { return sortPropertyValue; }
        }

        protected override ListSortDirection SortDirectionCore
        {
            get { return sortDirectionValue; }
        }

        /// <summary>
        /// Applies the sort to the <see cref="ListDataView"/> based on the object property name and <see cref="ListSortDirection"/> 
        /// </summary>
        /// <param name="propertyName">The property name of the object to sort</param>
        /// <param name="direction">The direction of the sort</param>
        /// <exception cref="ArgumentException"></exception>
        public void ApplySort(string propertyName, ListSortDirection direction)
        {
            // Check the properties of the list object for a property with the specified name.
            PropertyDescriptor prop = TypeDescriptor.GetProperties(typeof(T))[propertyName];

            // If the propertyName is null throw exception otherwise apply sort
            if (prop == null)
            {
                throw new ArgumentException(propertyName +
                    " is not a valid property for type:" + typeof(T).Name);
            }
            else
            {
                ApplySortCore(prop, direction);
            }
        }

        protected override void ApplySortCore(PropertyDescriptor prop,
            ListSortDirection direction)
        {

            sortedList = new ArrayList();

            /*
             * Check to see if the property type we are sorting by implements
             * the IComparable interface. 
             */
            Type interfaceType = prop.PropertyType.GetInterface("IComparable");

            if (interfaceType != null)
            {
                // Set the SortPropertyValue and SortDirectionValue.
                sortPropertyValue = prop;
                sortDirectionValue = direction;

                unsortedItems = new ListDataView<T>();

                if (sortPropertyValue != null)
                {
                    // Loop through each item, adding it the the sortedItems ArrayList.
                    foreach (Object item in this.Items)
                    {
                        unsortedItems.Add((T)item);
                        sortedList.Add(prop.GetValue(item));
                    }
                }
                // Call Sort on the ArrayList.
                sortedList.Sort();
                T temp;

                // Check the sort direction and then copy the sorted items back into the list.
                 
                if (direction == ListSortDirection.Descending)
                    sortedList.Reverse();

                for (int i = 0; i < this.Count; i++)
                {
                    int position = Find(prop.Name, sortedList[i]);
                    if (position != i && position > 0)
                    {
                        temp = this[i];
                        this[i] = this[position];
                        this[position] = temp;
                    }
                }

                isSortedValue = true;

                /* If the list does not have a filter applied, 
                 * raise the ListChanged event so bound controls refresh their values. 
                 * Pass -1 for the index since this is a Reset.
                 */
                if (String.IsNullOrEmpty(Filter))
                    OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
            }
            else
                // If the property type does not implement IComparable, throw an exception.
                throw new InvalidOperationException("Cannot sort by "
                    + prop.Name + ". This" + prop.PropertyType.ToString() +
                    " does not implement IComparable");
        }

        protected override void RemoveSortCore()
        {
            this.RaiseListChangedEvents = false;
            // Ensure the list has been sorted.
            if (unsortedItems != null && originalListValue.Count > 0)
            {
                this.Clear();
                if (Filter != null)
                {
                    unsortedItems.Filter = this.Filter;
                    foreach (T item in unsortedItems)
                        this.Add(item);
                }
                else
                {
                    foreach (T item in originalListValue)
                        this.Add(item);
                }
                isSortedValue = false;
                this.RaiseListChangedEvents = true;
                // Raise the list changed event, indicating a reset, and index of -1.
                OnListChanged(new ListChangedEventArgs(ListChangedType.Reset,
                    -1));
            }
        }
        /// <summary>
        /// Remove the List sort
        /// </summary>
        public void RemoveSort()
        {
            RemoveSortCore();
        }


        public override void EndNew(int itemIndex)
        {
            /* Check to see if the item is added to the end of the list,
             * and if so, re-sort the list.
            */
            if (IsSortedCore && itemIndex > 0
                && itemIndex == this.Count - 1)
            {
                ApplySortCore(this.sortPropertyValue,
                    this.sortDirectionValue);
                base.EndNew(itemIndex);
            }
        }

        #endregion Sorting

        #region AdvancedSorting
        public bool SupportsAdvancedSorting
        {
            get { return true; }
        }
        private ListSortDescriptionCollection _sortDescriptions;
        public ListSortDescriptionCollection SortDescriptions
        {
            get { return _sortDescriptions; }
        }
        /// <summary>
        /// Applies the sort to the <see cref="ListDataView"/> based on a <see cref="ListSortDescriptionCollection"/> and <see cref="ListSortDirection"/> 
        /// </summary>
        /// <param name="sorts">A collection of <see cref="ListSortDescription"/>.</param>
        public void ApplySort(ListSortDescriptionCollection sorts)
        {
            for (int i = 0; i < sorts.Count; i++)
            {
                ApplySortCore(sorts[i].PropertyDescriptor, sorts[i].SortDirection);
            }
        }

        #endregion AdvancedSorting

        #region Filtering

        public bool SupportsFiltering
        {
            get { return true; }
        }

        public void RemoveFilter()
        {
            if (Filter != null) Filter = null;
        }

        private string filterValue = null;
        /// <summary>
        /// Gets all filter values within Parentheses
        /// </summary>
        /// <param name="value">The filter string</param>
        /// <returns>A <see cref="List<string>"/> filled with the filter values within Parentheses</returns>
        private List<string> GetFilterParentheses(string value)
        {
            //@" ^.*?(?= and \(\[| or \(\[)|(?: and \(\[.*?| or \(\[.*?)(?=\) and |\) or |\)).+?(?= and |\;)"
            Regex orderOfOperation = new Regex(@"(?:\()?(?:(?: and \(| or \(|\))*)?(?>[^()]+|(?<c>)\(|(?<-c>)\))*(?(c)(?!))\)");


            string startValue = "";
            string endValue = "";
            if (value.StartsWith("and (")) { startValue = "and ("; value = Regex.Replace(value, @"^and \(", ""); }
            if (value.StartsWith("or (")) { startValue = "or ("; value = Regex.Replace(value, @"^or \(", ""); }
            if (value.StartsWith("((")) { startValue = "("; value = Regex.Replace(value, @"^\(", ""); }
            if (value.StartsWith("and ")) { startValue = "and "; value = Regex.Replace(value, "^and ", ""); }
            if (value.StartsWith("or ")) { startValue = "or "; value = Regex.Replace(value, "^or ", ""); }
            if (value.EndsWith("))")) { endValue = "))"; value = Regex.Replace(value, @"\)$", ""); }

            if (!value.EndsWith(";")) { value = value + ";"; }

            MatchCollection order = orderOfOperation.Matches(value);
            List<string> orderString = new List<string>();
            if (order != null)
            {
                int matchIndex = 0;
                foreach (Match match in order)
                {
                    string matchValue = match.Value.Trim(' ');

                    if (matchValue.EndsWith("))"))
                    {
                        //matchValue = Regex.Replace(value, @"\)$", "");
                        orderString.AddRange(GetFilterParentheses(matchValue));
                    }
                    else if (matchValue.StartsWith("((") || matchValue.StartsWith("and ((") || matchValue.StartsWith("or (("))
                    {
                        //matchValue = Regex.Replace(value, @"^or \(|^and \(|^\(", "");
                        orderString.AddRange(GetFilterParentheses(matchValue));
                    }
                    else
                    {
                        if (matchIndex == 0) { matchValue = startValue + match.Value; }
                        if (matchIndex == order.Count - 1) { matchValue = match.Value + endValue; }
                        orderString.Add(matchValue);
                    }
                    matchIndex++;
                }
            }
            else
            {
                orderString.Add(value);
            }
            //orderString.OrderBy(x => x.StartsWith("(")).ThenBy(x => x.EndsWith(")")).ThenBy(x => x.StartsWith("and")).ThenBy(x => x.StartsWith("or"));
            return orderString;
        }
        /// <summary>
        /// Gets the order of operations to apply the filter
        /// </summary>
        /// <param name="value">The filter string</param>
        /// <returns>A <see cref="List<FilterOrder<T>>"/> to apply the filter in order.</returns>
        private List<FilterOrder<T>> GetOrderOfOperation(string value)
        {
            List<string> orderString = GetFilterParentheses(value);
            if (value.EndsWith(";")) { value = Regex.Replace(value, @"\;$", ""); }


            Regex _filterHasAndOr = new Regex(@"^.*?(?= and \[| or \[|\;)|(?: and \[.*?| or \[.*?)$", RegexOptions.IgnoreCase);
            Regex _filterStartsAndOr = new Regex(@"(?:^.*?(?= and \[| or \[|$)$)|(?: and \[.*?| or \[.*?)(?=\;|$)", RegexOptions.IgnoreCase);
            string[] matches = Array.ConvertAll(value.Replace(";", "").Split(orderString.ToArray(), StringSplitOptions.RemoveEmptyEntries), x => x.Trim(' '));

            foreach (string m in matches)
            {
                if (_filterHasAndOr.IsMatchNotEmpty(m))
                {
                    orderString.AddRange(_filterHasAndOr.Matches(m).Cast<Match>().Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).ToArray().ToList());
                }
                else if (_filterStartsAndOr.IsMatchNotEmpty(m))
                {
                    orderString.AddRange(_filterStartsAndOr.Matches(m).Cast<Match>().Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).ToArray().ToList());
                }
            }
            orderString.Sort((x, y) => value.IndexOf(x, StringComparison.Ordinal).CompareTo(value.IndexOf(y, StringComparison.Ordinal)));
            List<string> test = new List<string>();
            for (int i = 0; i < orderString.Count; i++)
            {
                string m = orderString[i];
                if (_filterHasAndOr.IsMatchNotEmpty(m))
                {
                    test.AddRange(_filterHasAndOr.Matches(m).Cast<Match>().Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).ToArray().ToList());
                }
                else if (_filterStartsAndOr.IsMatchNotEmpty(m))
                {
                    test.AddRange(_filterStartsAndOr.Matches(m).Cast<Match>().Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).ToArray().ToList());
                }
            }
            List<FilterOrder<T>> filterOrders = new List<FilterOrder<T>>();
            for (int i = 0; i < test.Count; i++)
            {
                //get and or matches here to split up in order of operation
                string item = test[i];
                int k = value.IndexOf(item, StringComparison.Ordinal);
                item = item.Trim(' ');
                if (item.StartsWith("and ((")) { item = Regex.Replace(item, @"^and \(", "and "); }
                if (item.StartsWith("or ((")) { item = Regex.Replace(item, @"^or \(", "or "); }
                if (item.StartsWith("and (")) { item = Regex.Replace(item, @"^and \(", "and "); }
                if (item.StartsWith("or (")) { item = Regex.Replace(item, @"^or \(", "or "); }
                if (item.StartsWith("((")) { item = Regex.Replace(item, @"^\(", ""); }
                if (item.StartsWith("(")) { item = Regex.Replace(item, @"^\(", ""); }
                if (item.EndsWith("))")) { item = Regex.Replace(item, @"\)$", ""); }
                if (item.EndsWith(")")) { item = Regex.Replace(item, @"\)$", ""); }
                test[i] = item;
                string conjunctiveOperator = "";
                if (item.ToUpper().StartsWith("OR"))
                {
                    conjunctiveOperator = "OR";
                    item = Regex.Replace(item, @"^or ", "");
                }
                else if (item.ToUpper().StartsWith("AND"))
                {
                    conjunctiveOperator = "AND";
                    item = Regex.Replace(item, @"^and ", "");
                }
                item = item.Trim(' ');
                // Check to see if the filter was set previously.
                // Also, check if current filter is a subset of 
                // the previous filter.
                if (!String.IsNullOrEmpty(filterValue)
                        && !value.Contains(filterValue))
                    ResetList();

                // Parse and apply the filter.
                SingleFilterInfo filterInfo = ParseFilter(item);
                FilterOrder<T> filterOrder = new FilterOrder<T>(i, conjunctiveOperator, item, filterInfo);
                filterOrders.Add(filterOrder);
            }
            return filterOrders;
        }

        /// <summary>
        /// Gets or sets the filter string
        /// </summary>
        public string Filter
        {
            get
            {
                return filterValue;
            }
            set
            {
                if (filterValue == value) return;

                // If the value is not null or empty, but doesn't match expected format, throw an exception.
                if (!string.IsNullOrEmpty(value) &&
                    !Regex.IsMatch(value,
                    BuildRegExForFilterFormat(), RegexOptions.Singleline))
                    throw new ArgumentException("Filter is not in " +
                          "the format: propName[<>=LIKE]'value'.");

                //Turn off list-changed events.
                RaiseListChangedEvents = false;

                // If the value is null or empty, reset list.
                if (string.IsNullOrEmpty(value))
                    ResetList();
                else
                {
                    // Get the filter order of operations.
                    List<FilterOrder<T>> orderString = GetOrderOfOperation(value);
                    for (int ord = 0; ord < orderString.Count; ord++)
                    {
                        FilterOrder<T> match = orderString[ord];
                        SetFilterResults(orderString, ord);
                    }
                    // Finalize the filter after order of operations is complete.
                    ApplyFilter(orderString);
                }

                // Set the filter value and turn on list changed events.
                filterValue = value;
                RaiseListChangedEvents = true;
                OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
            }
        }


        // Build a regular expression to determine if filter is in correct format.
        private static string BuildRegExForFilterFormat()
        {
            StringBuilder regex = new StringBuilder();

            // Look for optional literal brackets, followed by word characters or space.
            regex.Append(@"\[?[\w\s]+\]?\s?");

            // Add the operators: > < = or  LIKE.
            regex.Append(@"[><=\b(LIKE)\b]");

            //Add optional space followed by optional quote and any character followed by the optional quote.
            regex.Append(@"\s?'?.+'?");

            return regex.ToString();
        }
        /// <summary>
        /// Resets the list
        /// </summary>
        private void ResetList()
        {
            this.ClearItems();
            foreach (T t in originalListValue)
                this.Items.Add(t);
            if (IsSortedCore)
                ApplySortCore(SortPropertyCore, SortDirectionCore);
        }


        protected override void OnListChanged(ListChangedEventArgs e)
        {
            /* If the list is reset, check for a filter.
             * If a filter is applied don't allow items to be added to the list.*/
             
            if (e.ListChangedType == ListChangedType.Reset)
            {
                if (Filter == null || Filter == "")
                    AllowNew = true;
                else
                    AllowNew = false;
            }
            // Add the new item to the original list.
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                List.Add(this[e.NewIndex]);
                if (!String.IsNullOrEmpty(Filter))
                {
                    string cachedFilter = this.Filter;
                    this.Filter = "";
                    this.Filter = cachedFilter;
                }
            }
            // Remove the new item from the original list.
            if (e.ListChangedType == ListChangedType.ItemDeleted)
                List.RemoveAt(e.NewIndex);

            base.OnListChanged(e);
        }
        /// <summary>
        /// Gets the filter results based on the order of operations
        /// </summary>
        /// <param name="filterOrders">A List of the filter order of operations</param>
        /// <param name="index">The index of the filter or to set</param>
        /// <exception cref="InvalidOperationException"></exception>
        internal void SetFilterResults(List<FilterOrder<T>> filterOrders, int index)
        {
            SingleFilterInfo filterParts = filterOrders[index].FilterInfo;
            List<T> results;

            // Check to see if the property type we are filtering by implements
            // the IComparable interface.
            Type interfaceType =
                TypeDescriptor.GetProperties(typeof(T))[filterParts.PropName]
                .PropertyType.GetInterface("IComparable");

            if (interfaceType == null)
                throw new InvalidOperationException("Filtered property" +
                " must implement IComparable.");

            results = new List<T>();
            List<T> searchList = new List<T>();
            switch (filterOrders[index].ConjunctiveOperator.ToUpper())
            {
                case "AND":
                    searchList = filterOrders[index - 1].Result;
                    break;
                case "OR":
                    results = filterOrders[index - 1].Result;
                    searchList = this.ToList();
                    break;
                default:
                    searchList = this.ToList();
                    break;
            }
            // Check each value and add to the results list.
            foreach (T item in searchList)
            {
                if (filterParts.PropDesc.GetValue(item) != null)
                {
                    if (filterParts.OperatorValue == FilterOperator.EqualTo || filterParts.OperatorValue == FilterOperator.NotEqualTo
                        || filterParts.OperatorValue == FilterOperator.LessThan || filterParts.OperatorValue == FilterOperator.LessThanOrEqualTo
                        || filterParts.OperatorValue == FilterOperator.GreaterThan || filterParts.OperatorValue == FilterOperator.GreaterThanOrEqualTo
                        || filterParts.OperatorValue == FilterOperator.None)
                    {
                        var i = item.GetType().GetProperty(filterParts.PropName).GetValue(item);
                        if (i.GetType() == typeof(string))
                        {
                            i = i.ToString().ToUpper();
                        }
                        IComparable compareValue =
                            i as IComparable;
                        //filterParts.PropDesc.GetValue(item) as IComparable;
                        int result =
                            compareValue.CompareTo(filterParts.CompareValue);
                        if (filterParts.OperatorValue ==
                            FilterOperator.EqualTo && result == 0)
                            if (!results.Contains(item))
                                results.Add(item);
                        if (filterParts.OperatorValue ==
                            FilterOperator.NotEqualTo && result > 0)
                            if (!results.Contains(item))
                                results.Add(item);
                        if (filterParts.OperatorValue ==
                            FilterOperator.GreaterThan && result > 0)
                            if (!results.Contains(item))
                                results.Add(item);
                        if (filterParts.OperatorValue ==
                            FilterOperator.LessThan && result < 0)
                            if (!results.Contains(item))
                                results.Add(item);
                        if (filterParts.OperatorValue ==
                            FilterOperator.GreaterThanOrEqualTo && result >= 0)
                            if (!results.Contains(item))
                                results.Add(item);
                        if (filterParts.OperatorValue ==
                            FilterOperator.LessThanOrEqualTo && result <= 0)
                            if (!results.Contains(item))
                                results.Add(item);
                    }
                    else if (filterParts.OperatorValue == FilterOperator.Like)
                    {
                        var i = item.GetType().GetProperty(filterParts.PropName).GetValue(item);
                        if (filterParts.CompareValue.ToString().Contains("%"))
                        {
                            if (filterParts.CompareValue.ToString().IndexOf("%") == 0 && filterParts.CompareValue.ToString().Count(x => x == '%') == 1)
                            {
                                if (filterParts.OperatorValue ==
                                    FilterOperator.Like && i.ToString().EndsWith(filterParts.CompareValue.ToString().Replace("%", "")))
                                    if (!results.Contains(item))
                                        results.Add(item);
                            }
                            else if (filterParts.CompareValue.ToString().IndexOf("%") == filterParts.CompareValue.ToString().Length && filterParts.CompareValue.ToString().Count(x => x == '%') == 1)
                            {
                                if (filterParts.OperatorValue ==
                                FilterOperator.Like && i.ToString().StartsWith(filterParts.CompareValue.ToString().Replace("%", "")))
                                    if (!results.Contains(item))
                                        results.Add(item);
                            }
                            else if (filterParts.CompareValue.ToString().Count(x => x == '%') > 1)
                            {
                                string val = filterParts.CompareValue.ToString().Replace("%", "%%").Trim('%');
                                if (filterParts.CompareValue.ToString().StartsWith("%")) { val = "%" + val; }
                                if (filterParts.CompareValue.ToString().EndsWith("%")) { val = val + "%"; }
                                string[] split = Regex.Split(val, @"((?<=[%]\G)|((?=[%])(?<=[%])))", RegexOptions.IgnorePatternWhitespace).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                                bool[] bools = new bool[split.Length];
                                for (int s = 0; s < split.Length; s++)
                                {
                                    if (split[s].StartsWith("%") && !split[s].EndsWith("%"))
                                    {
                                        bools[s] = i.ToString().EndsWith(split[s].Replace("%", ""));
                                    }
                                    else if (!split[s].StartsWith("%") && split[s].EndsWith("%"))
                                    {
                                        bools[s] = i.ToString().StartsWith(split[s].Replace("%", ""));
                                    }
                                    else
                                    {
                                        bools[s] = i.ToString().Contains(split[s].Replace("%", ""));
                                    }
                                }
                                if (bools.All(x => x == true))
                                    if (!results.Contains(item))
                                        results.Add(item);
                            }
                        }
                        else
                        {
                            if (filterParts.OperatorValue ==
                                FilterOperator.Like && i.ToString().Contains(filterParts.CompareValue.ToString()))
                                if (!results.Contains(item))
                                    results.Add(item);
                        }
                    }
                }
            }
            filterOrders[index].Result = results;
        }
        /// <summary>
        /// Applies the filter
        /// </summary>
        /// <param name="filterOrders">A List of the filter order of operations</param>
        internal void ApplyFilter(List<FilterOrder<T>> filterOrders)
        {
            List<T> results = filterOrders[filterOrders.Count - 1].Result;
            this.ClearItems();
            foreach (T itemFound in results)
                this.Add(itemFound);
        }

        internal SingleFilterInfo ParseFilter(string filterPart)
        {
            SingleFilterInfo filterInfo = new SingleFilterInfo();
            filterInfo.OperatorValue = DetermineFilterOperator(filterPart);

            // Split the string by the operator value
            string[] filterStringParts =
                filterPart.Split(new string[] { filterInfo.OperatorValue.ToDescriptionString() }, StringSplitOptions.RemoveEmptyEntries);

            // If the string did not split from the operator value use a regex expression
            if (filterStringParts.Length == 1)
            {
                filterStringParts =
                Regex.Split(filterPart, $@"(?<![\[\]])\b{filterInfo.OperatorValue.ToDescriptionString()}\b(?=[^\[\]]* ')(?![^\[]*'{filterInfo.OperatorValue.ToDescriptionString()}.*?')", RegexOptions.IgnoreCase);
            }
            
            // Set the filter property name
            filterInfo.PropName =
                filterStringParts[0].Replace("[", "").
                Replace("]", "").Replace(" AND ", "").Replace(" OR ", "").Trim();

            // Creates a new collection and assign it the properties for the list type.
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));

            // Sets a PropertyDescriptor to the specific property.
            PropertyDescriptor filterPropDesc = properties.Find(filterInfo.PropName, false);
            if (filterPropDesc == null)
            {
                filterPropDesc = properties.Find(filterInfo.PropName, true);
                if (filterPropDesc != null)
                {
                    filterInfo.PropName = filterPropDesc.Name;
                }
            }


            // Convert the filter compare value to the property type.
            if (filterPropDesc == null)
                throw new InvalidOperationException("Specified property to " +
                    "filter " + filterInfo.PropName +
                    " on does not exist on type: " + typeof(T).Name);

            // Set the filter property descriptor
            filterInfo.PropDesc = filterPropDesc;

            // Remove the quote from the filter value
            string comparePartNoQuotes = StripOffQuotes(filterStringParts[1]);
            try
            {
                // Set the filter compare value
                TypeConverter converter =
                    TypeDescriptor.GetConverter(filterPropDesc.PropertyType);
                filterInfo.CompareValue =
                    converter.ConvertFromString(comparePartNoQuotes);
            }
            catch (NotSupportedException)
            {
                throw new InvalidOperationException("Specified filter" +
                    "value " + comparePartNoQuotes + " can not be converted" +
                    "from string. Implement a type converter for " +
                    filterPropDesc.PropertyType.ToString());
            }
            return filterInfo;
        }

        internal FilterOperator DetermineFilterOperator(string filterPart)
        {
            // Determine the filter's operator.
            if (Regex.IsMatch(filterPart, "[^>^<] = "))
                return FilterOperator.EqualTo;
            else if (Regex.IsMatch(filterPart, "[^>^<] != "))
                return FilterOperator.NotEqualTo;
            else if (Regex.IsMatch(filterPart, " < [^>^=]"))
                return FilterOperator.LessThan;
            else if (Regex.IsMatch(filterPart, "[^<] > [^=]"))
                return FilterOperator.GreaterThan;
            else if (Regex.IsMatch(filterPart, " <= [^>^=]"))
                return FilterOperator.LessThanOrEqualTo;
            else if (Regex.IsMatch(filterPart, "[^<] >= [^=]"))
                return FilterOperator.GreaterThanOrEqualTo;
            else if (Regex.IsMatch(filterPart, "[^<](?i)(?: LIKE )[^=]"))
                return FilterOperator.Like;
            else
                return FilterOperator.None;
        }

        internal static string StripOffQuotes(string filterPart)
        {
            // Strip off quotes in compare value if they are present.
            if (Regex.IsMatch(filterPart, "'.+'"))
            {
                int quote = filterPart.IndexOf('\'');
                filterPart = filterPart.Remove(quote, 1);
                quote = filterPart.LastIndexOf('\'');
                filterPart = filterPart.Remove(quote, 1);
                filterPart = filterPart.Trim();
            }
            return filterPart.ToUpper();
        }

        #endregion Filtering
    }
    /// <summary>
    /// An Object to hold the filter information and list of results
    /// </summary>
    /// <typeparam name="T">The object type of the list</typeparam>
    public class FilterOrder<T>
    {
        public int Order { get; set; }
        public string ConjunctiveOperator { get; set; }
        public string FilterString { get; set; }
        public SingleFilterInfo FilterInfo { get; set; }
        public List<T> Result { get; set; } = new List<T>();
        public FilterOrder(int order, string conjunctiveOperator, string filterString)
        {
            Order = order;
            ConjunctiveOperator = conjunctiveOperator;
            FilterString = filterString;
        }
        public FilterOrder(int order, string conjunctiveOperator, SingleFilterInfo singleFilterInfo)
        {
            Order = order;
            ConjunctiveOperator = conjunctiveOperator;
            FilterInfo = singleFilterInfo;
        }
        public FilterOrder(int order, string conjunctiveOperator, string filterString, SingleFilterInfo singleFilterInfo)
        {
            Order = order;
            ConjunctiveOperator = conjunctiveOperator;
            FilterString = filterString;
            FilterInfo = singleFilterInfo;
        }
    }
    /// <summary>
    /// Structure to hold the single filter view
    /// </summary>
    public struct SingleFilterInfo
    {
        internal string PropName;
        internal PropertyDescriptor PropDesc;
        internal Object CompareValue;
        internal FilterOperator OperatorValue;
    }

    /// <summary>
    /// Enum to hold filter operators. The description is the string value
    /// </summary>
    public enum FilterOperator
    {
        [Description("=")]
        EqualTo,
        [Description("!=")]
        NotEqualTo,
        [Description("<")]
        LessThan,
        [Description(">")]
        GreaterThan,
        [Description(" ")]
        None = ' ',
        [Description("<=")]
        LessThanOrEqualTo,
        [Description(">=")]
        GreaterThanOrEqualTo,
        [Description("LIKE")]
        Like

    }
    /// <summary>
    /// A class to allow an extension for the <see cref="FilterOperator"/> to get the string description
    /// </summary>
    public static class FilterOperatorExtensions
    {
        /// <summary>
        /// Gets the <see cref="FilterOperator"/> description
        /// </summary>
        /// <param name="val">The <see cref="FilterOperator"/> value</param>
        /// <returns>The description of the <see cref="FilterOperator"/>.</returns>
        public static string ToDescriptionString(this FilterOperator val)
        {
            DescriptionAttribute[] attributes = (DescriptionAttribute[])val.GetType().GetField(val.ToString())
               .GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : string.Empty;
        }
    }
    /// <summary>
    /// A class to allow an extension for the <see cref="Regex"/> to check if the match is empty
    /// </summary>
    public static class RegexExtensions
    {
        /// <summary>
        /// Check if the <see cref="Regex"/> <see cref="Match"/>s are empty
        /// </summary>
        /// <param name="regex"></param>
        /// <param name="value"></param>
        /// <returns>A true or false value</returns>
        public static bool IsMatchNotEmpty(this Regex regex, string value)
        {
            bool isMatched = false;
            if (regex.IsMatch(value))
            {
                foreach (Match match in regex.Matches(value))
                {
                    if (match.Value.Length > 0)
                    {
                        isMatched = true;
                    }
                }
            }
            return isMatched;
        }
    }
}
