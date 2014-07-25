﻿using System;
using System.Collections.Generic;
using VDS.RDF.Nodes;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Query.Elements;
using VDS.RDF.Query.Engine;
using VDS.RDF.Query.Results;

namespace VDS.RDF.Query.Compiler
{
    public class CompilingElementVisitor
        : IElementVisitor
    {
        public CompilingElementVisitor()
        {
            this.Algebras = new Stack<IAlgebra>();
            this.Algebras.Push(Table.CreateUnit());
        }

        private Stack<IAlgebra> Algebras { get; set; }

        public IAlgebra Compile(IElement element)
        {
            element.Accept(this);
            if (this.Algebras.Count != 1) throw new RdfQueryException(String.Format("Element compilation failed, expected to produce 1 algebra but produced {0}", this.Algebras.Count));
            return this.Algebras.Pop();
        }

        public void Visit(BindElement bind)
        {
            this.Algebras.Push(Extend.Create(this.Algebras.Pop(), bind.Assignments));
        }

        public void Visit(DataElement data)
        {
            this.Algebras.Push(Join.Create(this.Algebras.Pop(), new Table(CompileInlineData(data.Data))));
        }

        public void Visit(FilterElement filter)
        {
            this.Algebras.Push(Filter.Create(this.Algebras.Pop(), filter.Expressions));
        }

        public void Visit(GroupElement group)
        {
            throw new NotImplementedException();
        }

        public void Visit(MinusElement minus)
        {
            throw new NotImplementedException();
        }

        public void Visit(NamedGraphElement namedGraph)
        {
            namedGraph.Element.Accept(this);
            this.Algebras.Push(new NamedGraph(namedGraph.Graph, this.Algebras.Pop()));
        }

        public void Visit(OptionalElement optional)
        {
            throw new NotImplementedException();
        }

        public void Visit(PathBlockElement pathBlock)
        {
            throw new NotImplementedException();
        }

        public void Visit(ServiceElement service)
        {
            service.InnerElement.Accept(this);
            this.Algebras.Push(new Service(this.Algebras.Pop(), service.EndpointUri, service.IsSilent));
        }

        public void Visit(SubQueryElement subQuery)
        {
            throw new NotImplementedException();
        }

        public void Visit(TripleBlockElement tripleBlock)
        {
// ReSharper disable RedundantCast
            IAlgebra bgp = tripleBlock.Triples.Count > 0 ? (IAlgebra) new Bgp(tripleBlock.Triples) : (IAlgebra) Table.CreateUnit();
// ReSharper restore RedundantCast
            this.Algebras.Push(Join.Create(this.Algebras.Pop(), bgp));
        }

        public void Visit(UnionElement union)
        {
            // Firstly convert all the elements
            foreach (IElement element in union.Elements)
            {
                CompilingElementVisitor compiler = new CompilingElementVisitor();
                this.Algebras.Push(compiler.Compile(element));
            }

            // Then union together the results
            IAlgebra current = this.Algebras.Pop();
            for (int i = 1; i < union.Elements.Count; i++)
            {
                current = new Union(this.Algebras.Pop(), current);
            }
            this.Algebras.Push(Join.Create(this.Algebras.Pop(), current));
        }

        /// <summary>
        /// Compiles the result rows provided for inline data into the format needed for algebra
        /// </summary>
        /// <param name="rows">Rows</param>
        /// <returns>Inline Data</returns>
        public static IEnumerable<ISolution> CompileInlineData(IEnumerable<IResultRow> rows)
        {
            foreach (IResultRow r in rows)
            {
                Solution s = new Solution();
                foreach (String var in r.Variables)
                {
                    INode n;
                    if (r.TryGetBoundValue(var, out n)) s.Add(var, n);
                }
                yield return s;
            }
        }
    }
}