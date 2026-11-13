---
description: "Guidelines for C# XML documentation comments, NuGet package descriptions, and package README files."
applyTo: "{**/*.cs,src/**/README.md,src/**/*.csproj}"
---

- **Read the `coding.instructions.md` first**.
  Instructions in this file are incomplete without them.

# Documentation guidance

- Write for intermediate to advanced C# developers.
- Write documentation for the users, not the maintainers.
- Make documentation as concise as possible without losing information.
- Change phrasing to drop redundant words, for example `Returns red apples.` over `Returns apples of red color.`
- Focus on the "why". The "how" should be self-explanatory from the API itself.
- Always try to simplify APIs before resorting to documenting them.

## NuGet packages

### Description (.csproj)

- Start with what the package provides, not "This package contains...".
- Keep it to one sentence — NuGet.org truncates long descriptions in search results.

### README.md

- Use the package ID as the heading.
- Structure: purpose, key types, usage example, documentation links.
- Keep examples minimal — just enough to show how to get started.
- Mark internal packages (`*.Internal`) as not intended for direct consumption.

## C# XML comments

- Document `public` and `protected` members with [XML comments](https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/documentation-comments).
- Document internal members only if they are complex or not self-explanatory.
- Verify XML comments by building the project before committing changes.
- When removing unnecessary XML comments, preserve significant information as regular comments.
- Use `<summary>` to provide a brief description of what the type or member does. Start the summary with a present-tense, third-person verb.
- Use `<remarks>` for additional information, such as usage notes relevant to consumers.
  - Remove `<remarks>` if it restates documented-elsewhere or self-evident information. This is an optional element.
  - Don't document implementation details of the API not relevant to consumers of the library.
  - Don't document inheritor instructions for APIs not meant to be extended by consumers of the library.
- Use `<see langword="..."/>` for language-specific keywords like `null`, `true`, `false`, `int`, `bool`, etc.
- Use `<c>` for inline code snippets.
- Use `<example>` for usage examples on how to use the member.
  - Use `<code>` for code blocks. `<code>` tags should be placed within an `<example>` tag.
    Add the language of the code example using the `language` attribute, for example, `<code language="csharp">`.
- Use `<see cref="..."/>` to reference other types or members inline (in a sentence).
- Use `<seealso cref="..."/>` for standalone (not in a sentence) references to other types or members in the "See also" section of the online docs.
- Use `<para>` when multiple paragraphs are needed to make a documentation section readable.
  - Never use `<para>` in single-paragraph sections.
- For generic overloads, don't add "strongly-typed" or similar qualifiers to distinguish them from non-generic overloads. Let the type parameter references speak for themselves.
- _Prefer short type names in the `cref="..."` references_. 
  - Avoid fully-qualified references, e.g. `<seealso cref="Fully.Qualified.Foo"/>`.
  - If the type's namespace is not imported, add a `using` directive, e.g. `using Fully.Qualified; /// <seealso cref="Foo"/>`
  - If the type name appears in multiple imported namespaces, but used only from one, add an alias to disambiguate, e.g.
    `using Foo = Fully.Qualified.Foo; /// <seealso cref="Foo"/>`.
  - If the type is an open generic and cannot be aliased, use the shortest qualification needed to disambiguate, e.g.
    `using Fully; /// <seealso cref="Qualified.Foo{T}"/>`
- _Prefer `<see cref="..."/>` over plain text when referring to domain concepts with first-class abstractions_.
  E.g. `Sets the specified <see cref="Transaction"/> as the ambient transaction`. The first reference becomes a hyperlink
  users can follow, while the subsequent mentions remain plain text to keep the docs easy to read.

### Methods

- Use `<summary>` to describe _what_ the method does, not _how_ it is implemented.
  - For methods that return a value
    - Use template `{what it does} and returns {what it returns}`. E.g. `Foo.TryParse(string s, out Foo result)` summary should be
      ```xml
      Tries to parse <paramref name="s"/> into a <see cref="Foo"/> <paramref name="result"/> and returns <see langword="true"/>
      if it was successfully parsed; otherwise returns <see langword="false"/>.
      ```
    - For methods where action and result are logically the same, use template `Returns {what it returns}`.
      E.g. `Foo.Parse(string s)` summary should be
      ```xml
      Returns a <see cref="Foo"/> object parsed from the string representation in <paramref name="s"/>.
      ```
  - For methods returning `Task` or `Task<T>`:
    - Start `<summary>` with `Asynchronously`, followed by _a present-tense, third-person verb_ as described above.
    - Don't describe non-generic `Task` results - such methods are asynchronous `void` equivalents.
    - Describe `T` instead of `Task<T>` results - such methods are asynchronous value-returning equivalents.
- Use `<returns>` to describe what the method returns, but only if it's not redundant.
  - Remove `<returns>` if it restates documented-elsewhere or self-evident information. This is an optional element.
  - Try improving the method name and return type before documenting it.
  - The description should be a noun phrase that doesn't specify the data type.
  - Begin with an introductory article.
  - If the return type is Boolean, the wording should be of the form "`<see langword="true" />` if ...; otherwise, `<see langword="false" />`.".

### Parameters

- Remove `<param>` elements that restate information evident from the parameter types and names.
  This is an optional element, but if one parameter needs docs, C# compiler requires them for all parameters.
- Improve parameter types and names before documenting them.
- Use `<param name="...">` to describe method parameters.
  - The description should be a noun phrase that doesn't specify the data type.
  - Begin with an introductory article.
  - If the parameter is a flag enum, start the description with "A bitwise combination of the enumeration values that specifies...".
  - If the parameter is a non-flag enum, start the description with "One of the enumeration values that specifies...".
  - If the parameter is a Boolean, the wording should be of the form "`<see langword="true" />` to ...; otherwise, `<see langword="false" />`.".
  - If the parameter is an "out" parameter, the wording should be of the form
    `When this method returns, contains .... This parameter is treated as uninitialized.`.
- Use `<paramref name="...">` to reference parameter names in documentation.

### Type Parameters

- Use `<typeparam name="...">` to describe type parameters in generic types or methods, but only if they're not redundant.
  - Remove all `<typeparam>` elements if they restate documented-elsewhere or self-evident information. This is an optional
    element.
- Use `<typeparamref name="...">` to reference type parameters in documentation.

### Constructors

- The summary wording should be "Initializes a new instance of the <see cref="..."/> class (or struct).".
- Don't create default constructors just to document them.

### Properties

- The `<summary>` should start with:
  - "Gets or sets..." for a read-write property.
  - "Gets..." for a read-only property.
  - "Sets..." for a set-only property.
  - "Gets [or sets] a value that indicates whether..." for properties that return a Boolean value.
- Use `<value>` to describe the value of the property, but only if it's not redundant.
  - Remove `<value>` if it restates documented-elsewhere or self-evident information. This is an optional element.
  - The description should be a noun phrase that doesn't specify the data type.
  - If the property has a default value, add it in a separate sentence, for example, "The default is `<see langword="false" />`".
  - If the value type is Boolean, the wording should be of the form "`<see langword="true" />` if ...; otherwise, `<see langword="false" />`. The default is ...".

### Events

- The `<summary>` should start with "Occurs when...".

### Exceptions

- Use `<exception cref="...">` to document exceptions thrown by constructors, properties, indexers, methods, operators, and events.
- Document all exceptions thrown directly by the member.
- For exceptions thrown by the callees of the member
  - Document the API-level exceptions users are likely to encounter. E.g. a member passing an argument to a callee that
    validates it and throws `ArgumentNullException` should have it documented.
  - Don't document the low-level implementation exceptions that can be thrown by callees. E.g. don't document `ArithmeticException`
    or its descendants.
  - For missing validations that allow low-level exceptions to escape, see [Bugs](#bugs).
- Sort exceptions by each element's opening line, not by type name, to reduce merge conflicts. This places `<exception cref="...">`
  sorted by type name first, followed by `<inheritdoc path="/exception[@cref='T:{FullName}']"/>` sorted by full type name
  second.
  - Sort the way VS Code's _Sort Lines Ascending_ does: compare letters first and use case only to break ties, e.g. `InvalidOperationException`
    before `IOException`. Ordinal sorting produces a different order.
  - Keep each element on a single line when practical so users can re-sort them with VS Code's _Sort Lines Ascending_.
  - When any element spans multiple lines, reorder complete elements without sorting their individual lines.
- Don't document exceptions without evidence they are actually thrown, except when documenting intended behavior for a known
  bug demonstrated by an explicit bug test (see [Bugs](#bugs)). Symmetry with other APIs is irrelevant and doesn't justify
  lack of evidence.
- Describe the condition under which it's thrown.
  - Omit "Thrown if ..." or "If ..." at the beginning of the sentence.

### InheritDoc

- _Convert duplicated documentation elements to `<inheritdoc/>`_.
- _Document interfaces and base classes; use `<inheritdoc/>` in implementations and overrides_.
  - If behavior doesn't apply to every implementation or override, don't document it in the interface or base class.
- _Document members with the **most** parameters; use `<inheritdoc cref="..."/>` in simplifying overloads/extensions_. The
  C# compiler doesn't recognize `<inheritdoc>` as an equivalent of the `<param>` elements and forces all parameters to be
  documented by the same element type. Documenting methods with the most parameters allows reusing their docs for parameters
  in simpler overloads, but not the other way around.
- _Specify `path` to reuse specific XML elements rather than the entire doc_.
  - Place `path` before `cref` to make `<inheritdoc>` more readable when mixed with regular elements.
  - Use `<inheritdoc path="/summary" cref="..."/>` to reuse `<summary>`.
  - Use `<inheritdoc path="/remarks" cref="..."/>` to reuse `<remarks>`.
  - Use `<inheritdoc path="/param" cref="..."/>` to reuse all `<param>` elements.
  - Use `<inheritdoc path="/param[@name='{name}']" cref="..."/>` to reuse a specific `<param name="{name}">`.
  - Use `<inheritdoc path="/typeparam" cref="..."/>` to reuse all `<typeparam>` elements.
  - Use `<inheritdoc path="/typeparam[@name='{name}']" cref="..."/>` to reuse a specific `<typeparam name="{name}">`.
  - Use `<inheritdoc path="/exception" cref="..."/>` to reuse all `<exception>` elements.
  - Use `<inheritdoc path="/exception[@cref='T:{FullName}']" cref="..."/>` to reuse a specific `<exception cref="{Name}">`.
- _Document the differences instead of using `<inheritdoc/>` for elements with a major behavior difference_.

### Bugs

- Use XML doc comments to describe the intended behavior, not the buggy behavior.
- For explicit bug-test policy, see `test.instructions.md`.
- Verify that bug fixes match the documented intended behavior.
