﻿using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Support;
using System;
using System.Collections;

namespace Lucene.Net.Queries.Function
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */
    
    /// <summary>
    /// Instantiates <seealso cref="FunctionValues"/> for a particular reader.
    /// <br>
    /// Often used when creating a <seealso cref="FunctionQuery"/>.
    /// 
    /// 
    /// </summary>
    public abstract class ValueSource
    {
        /// <summary>
        /// Gets the values for this reader and the context that was previously
        /// passed to CreateWeight()
        /// </summary>
        public abstract FunctionValues GetValues(IDictionary context, AtomicReaderContext readerContext);

        public override abstract bool Equals(object o);

        public override abstract int GetHashCode();

        /// <summary>
        /// description of field, used in explain()
        /// </summary>
        public abstract string GetDescription();

        public override string ToString()
        {
            return GetDescription();
        }


        /// <summary>
        /// Implementations should propagate CreateWeight to sub-ValueSources which can optionally store
        /// weight info in the context. The context object will be passed to GetValues()
        /// where this info can be retrieved.
        /// </summary>
        public virtual void CreateWeight(IDictionary context, IndexSearcher searcher)
        {
        }

        /// <summary>
        /// Returns a new non-threadsafe context map.
        /// </summary>
        public static IDictionary NewContext(IndexSearcher searcher)
        {
            var context = new Hashtable(new IdentityComparer());
            context["searcher"] = searcher;
            return context;
        }


        //
        // Sorting by function
        //

        /// <summary>
        /// EXPERIMENTAL: This method is subject to change.
        /// <para>
        /// Get the SortField for this ValueSource.  Uses the <seealso cref="#GetValues(java.util.Map, AtomicReaderContext)"/>
        /// to populate the SortField.
        /// 
        /// </para>
        /// </summary>
        /// <param name="reverse"> true if this is a reverse sort. </param>
        /// <returns> The <seealso cref="org.apache.lucene.search.SortField"/> for the ValueSource </returns>
        public virtual SortField GetSortField(bool reverse)
        {
            return new ValueSourceSortField(this, reverse);
        }

        internal class ValueSourceSortField : SortField
        {
            private readonly ValueSource outerInstance;

            public ValueSourceSortField(ValueSource outerInstance, bool reverse)
                : base(outerInstance.GetDescription(), SortFieldType.REWRITEABLE, reverse)
            {
                this.outerInstance = outerInstance;
            }

            public override SortField Rewrite(IndexSearcher searcher)
            {
                var context = NewContext(searcher);
                outerInstance.CreateWeight(context, searcher);
                return new SortField(Field, new ValueSourceComparerSource(outerInstance, context), IsReverse);
            }
        }

        internal class ValueSourceComparerSource : FieldComparerSource
        {
            private readonly ValueSource outerInstance;

            private readonly IDictionary context;

            public ValueSourceComparerSource(ValueSource outerInstance, IDictionary context)
            {
                this.outerInstance = outerInstance;
                this.context = context;
            }

            public override FieldComparer NewComparer(string fieldname, int numHits, int sortPos, bool reversed)
            {
                return new ValueSourceComparer(outerInstance, context, numHits);
            }
        }

        /// <summary>
        /// Implement a <seealso cref="org.apache.lucene.search.FieldComparer"/> that works
        /// off of the <seealso cref="FunctionValues"/> for a ValueSource
        /// instead of the normal Lucene FieldComparer that works off of a FieldCache.
        /// </summary>
        internal class ValueSourceComparer : FieldComparer<double?>
        {
            private readonly ValueSource outerInstance;

            private readonly double[] values;
            private FunctionValues docVals;
            private double bottom;
            private readonly IDictionary fcontext;
            private double topValue;

            internal ValueSourceComparer(ValueSource outerInstance, IDictionary fcontext, int numHits)
            {
                this.outerInstance = outerInstance;
                this.fcontext = fcontext;
                values = new double[numHits];
            }

            public override int Compare(int slot1, int slot2)
            {
                return values[slot1].CompareTo(values[slot2]);
            }

            public override int CompareBottom(int doc)
            {
                return bottom.CompareTo(docVals.DoubleVal(doc));
            }

            public override void Copy(int slot, int doc)
            {
                values[slot] = docVals.DoubleVal(doc);
            }

            public override FieldComparer SetNextReader(AtomicReaderContext context)
            {
                docVals = outerInstance.GetValues(fcontext, context);
                return this;
            }

            public override void SetBottom(int slot)
            {
                this.bottom = values[slot];
            }

            public override void SetTopValue(object value)
            {
                this.topValue = (double)value;
            }

            // LUCENENET NOTE: This was value(int) in Lucene.
            public override IComparable this[int slot]
            {
                get { return values[slot]; }
            }

            public override int CompareTop(int doc)
            {
                double docValue = docVals.DoubleVal(doc);
                return topValue.CompareTo(docValue);
            }
        }
    }
}