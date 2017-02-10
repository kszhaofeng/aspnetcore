﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Evolution.Intermediate;
using Microsoft.AspNetCore.Razor.Evolution.CodeGeneration;

namespace Microsoft.AspNetCore.Razor.Evolution
{
    public abstract class DocumentClassifierPassBase : RazorIRPassBase
    {
        protected abstract string DocumentKind { get; }

        public override int Order => RazorIRPass.DocumentClassifierOrder;

        public sealed override DocumentIRNode ExecuteCore(RazorCodeDocument codeDocument, DocumentIRNode irDocument)
        {
            if (irDocument.DocumentKind != null)
            {
                return irDocument;
            }

            if (!IsMatch(codeDocument, irDocument))
            {
                return irDocument;
            }

            irDocument.DocumentKind = DocumentKind;
            irDocument.Target = CreateTarget(codeDocument, irDocument.Options);

            Rewrite(codeDocument, irDocument);

            return irDocument;
        }

        private void Rewrite(RazorCodeDocument codeDocument, DocumentIRNode irDocument)
        {
            // Rewrite the document from a flat structure to use a sensible default structure,
            // a namespace and class declaration with a single 'razor' method.
            var children = new List<RazorIRNode>(irDocument.Children);
            irDocument.Children.Clear();

            var @namespace = new NamespaceDeclarationIRNode()
            {
                //Content = "GeneratedNamespace",
            };

            var @class = new ClassDeclarationIRNode()
            {
                //AccessModifier = "public",
                //Name = "GeneratedClass",
            };

            var method = new RazorMethodDeclarationIRNode()
            {
                //AccessModifier = "public",
                // Modifiers = new List<string>() { "async" },
                //Name = "Execute",
                //ReturnType = "Task",
            };

            var documentBuilder = RazorIRBuilder.Create(irDocument);

            var namespaceBuilder = RazorIRBuilder.Create(documentBuilder.Current);
            namespaceBuilder.Push(@namespace);

            var classBuilder = RazorIRBuilder.Create(namespaceBuilder.Current);
            classBuilder.Push(@class);

            var methodBuilder = RazorIRBuilder.Create(classBuilder.Current);
            methodBuilder.Push(method);

            var visitor = new Visitor(documentBuilder, namespaceBuilder, classBuilder, methodBuilder);

            for (var i = 0; i < children.Count; i++)
            {
                visitor.Visit(children[i]);
            }

            // Note that this is called at the *end* of rewriting so that user code can see the tree
            // and look at its content to make a decision.
            OnDocumentStructureCreated(codeDocument, @namespace, @class, method);
        }

        protected abstract bool IsMatch(RazorCodeDocument codeDocument, DocumentIRNode irDocument);

        private RuntimeTarget CreateTarget(RazorCodeDocument codeDocument, RazorParserOptions options)
        {
            return RuntimeTarget.CreateDefault(codeDocument, options, (builder) => ConfigureTarget(codeDocument, builder));
        }

        protected virtual void ConfigureTarget(RazorCodeDocument codeDocument, IRuntimeTargetBuilder builder)
        {
            // Intentionally empty.
        }

        protected virtual void OnDocumentStructureCreated(
            RazorCodeDocument codeDocument,
            NamespaceDeclarationIRNode @namespace,
            ClassDeclarationIRNode @class,
            RazorMethodDeclarationIRNode @method)
        {
            // Intentionally empty.
        }

        private class Visitor : RazorIRNodeVisitor
        {
            private readonly RazorIRBuilder _document;
            private readonly RazorIRBuilder _namespace;
            private readonly RazorIRBuilder _class;
            private readonly RazorIRBuilder _method;

            public Visitor(RazorIRBuilder document, RazorIRBuilder @namespace, RazorIRBuilder @class, RazorIRBuilder method)
            {
                _document = document;
                _namespace = @namespace;
                _class = @class;
                _method = method;
            }

            public override void VisitChecksum(ChecksumIRNode node)
            {
                _document.Insert(0, node);
            }

            public override void VisitUsingStatement(UsingStatementIRNode node)
            {
                _namespace.AddAfter<UsingStatementIRNode>(node);
            }

            public override void VisitDeclareTagHelperFields(DeclareTagHelperFieldsIRNode node)
            {
                _class.Insert(0, node);
            }

            public override void VisitDefault(RazorIRNode node)
            {
                _method.Add(node);
            }
        }
    }
}
