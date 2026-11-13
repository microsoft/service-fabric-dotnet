---
description: "Guidelines for C# tests."
applyTo: "test/**/*.cs"
---

- **Read the `csharp.instructions.md` first**.
  Instructions in this file are incomplete without them.

# Testing Guidelines

## Terminology

- **SUT** means system under test. In C#, it's a single type with behavior, typically a `class` or a `struct`.
- **Target** means _member of the SUT_. In C#, it's a single constructor, method, property or event.

## Test Projects

- **`test` sub-folders containing test projects should have the same names as the `src` sub-folders**.
  For example, the `/test/Actors/` folder contains tests for the `/src/Actors/` product code.
- **Test projects should have the same name as their respective product projects, followed by the `.Tests` suffix**.
  For example, the `Microsoft.ServiceFabric.Actors.Tests.csproj` tests the `Microsoft.ServiceFabric.Actors.csproj`.
  Note that plural `Tests` in the project name indicates that it has multiple _tests_.
- **Test projects should use the product namespace, without the `Tests` suffix**.
  This helps to reduce the number of namespaces and using directives. For example, the `Microsoft.ServiceFabric.Actors.Tests.csproj`
  test classes should be in the `Microsoft.ServiceFabric.Actors` namespace.
- **Each top-level `public` or `internal` product type with observable behavior should have a separate test class `{TypeUnderTest}Test`**.
  Note that singular `Test` in the class name indicates that each test creates a separate instance of the test class.
  For example, top-level product class `ActorBase` should have a test class `ActorBaseTest`.
- **Each nested non-private product type with observable behavior should have a nested test class**.
  This helps to maintain test code structure consistent with the product code structure and reinforces discouragement of
  nested public types by the .NET Framework design guidelines. For example, nested type `Outer.Inner` should have a nested
  test class `OuterTest.InnerTest`.
  - The nested test class should not inherit from the parent test class unless the nested product class inherits from the
    parent product class as well.
  - Nested test classes should use the same structure described below as the regular test classes.

## Test Classes

- **Use nested test classes to mirror SUT structure**
  ```csharp
  namespace Microsoft.ServiceFabric.Diagnostics.Tracing;

  sealed class Trace : ITrace, IEquatable<Trace>
  {
      readonly string type;
      readonly string id;
      readonly ITextEventSource events;

      internal Trace(Type type, ServiceContext context, ITextEventSource events)
      {
          this.type = (type ?? throw new ArgumentNullException(nameof(type))).Name;
          id = TraceId(context ?? throw new ArgumentNullException(nameof(context)));
          this.events = events ?? throw new ArgumentNullException(nameof(events));
      }

      void ITrace.Error(string message) =>
          events.ErrorText(id, type, message);

      static string TraceId(ServiceContext context) =>
          FormattableString.Invariant($"{context.PartitionId:B}:{context.ReplicaOrInstanceId}");

      bool IEquatable<Trace>.Equals(Trace other) =>
          other != null && type == other.type && id == other.id && events == other.events;
  }
  ```

  The test class should be structured similar to this:

  ```csharp
  namespace Microsoft.ServiceFabric.Diagnostics.Tracing;

  public abstract class TraceTest
  {
      readonly ITrace sut;

      // Constructor parameters
      readonly Type type = fuzzy.Type();
      readonly ServiceContext context = fuzzy.ServiceContext();
      readonly ITextEventSource events = Mock.Of<ITextEventSource>();

      static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

      TraceTest() =>
          sut = new Trace(type, context, events);

      public sealed class Constructor : TraceTest
      {
          [Fact]
          public void ThrowsArgumentNullExceptionWhenTypeIsNull()
          {
              var exception = Assert.Throws<ArgumentNullException>(() => new Trace(null, context, events));
              Assert.Equal(nameof(type), exception.ParamName);
          }

          [Fact]
          public void ThrowsArgumentNullExceptionWhenContextIsNull()
          {
              var exception = Assert.Throws<ArgumentNullException>(() => new Trace(type, null, events));
              Assert.Equal(nameof(context), exception.ParamName);
          }

          [Fact]
          public void ThrowsArgumentNullExceptionWhenEventsIsNull()
          {
              var exception = Assert.Throws<ArgumentNullException>(() => new Trace(type, context, null));
              Assert.Equal(nameof(events), exception.ParamName);
          }
      }

      public new sealed class Equals : TraceTest
      {
          new readonly IEquatable<Trace> sut;
          readonly Trace other;

          public Equals()
          {
              sut = (IEquatable<Trace>)base.sut;
              other = new Trace(type, context, events);
          }

          [Fact]
          public void ReturnsTrueWhenTypeContextAndEventsAreSame() =>
              Assert.True(sut.Equals(other));

          [Fact]
          public void ReturnsFalseWhenOtherIsNull() =>
              Assert.False(sut.Equals(null));

          [Fact]
          public void ReturnsFalseWhenTypeIsDifferent() =>
              Assert.False(sut.Equals(new Trace(fuzzy.Type(), context, events)));

          [Fact]
          public void ReturnsFalseWhenContextIsDifferent() =>
              Assert.False(sut.Equals(new Trace(type, fuzzy.ServiceContext(), events)));

          [Fact]
          public void ReturnsFalseWhenEventsAreDifferent() =>
              Assert.False(sut.Equals(new Trace(type, context, Mock.Of<ITextEventSource>())));
      }

      public sealed class Error : TraceTest
      {
          // Method parameters
          readonly string message = fuzzy.String();

          readonly string traceId;

          public Error() =>
              traceId = context.PartitionId.ToString("B") + ":" + context.ReplicaOrInstanceId.ToString(CultureInfo.InvariantCulture);

          [Fact]
          public void EmitsErrorTextEvent()
          {
              sut.Error(message);

              Mock.Get(events).Verify(_ => _.ErrorText(traceId, type.Name, message), Times.Once);
              Mock.Get(events).Verify(_ => _.ErrorText(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                  Times.Once);
          }
      }
  }
  ```

- **Make the base class `abstract`** to communicate that it has no test methods of its own.

- **Create a readonly field called `sut` at the top of the class** to help reader understand what is being tested.
  - _Choose `sut` type based on the primary interface of the SUT_. If the SUT implements and is consumed through a specific
    interface, the variable should be of that interface type. This helps to better describe the SUT usage. Secondary interfaces,
    such as `IEquatable<T>`, don't change the primary interface; nested classes testing them narrow `sut` by casting `base.sut`
    to the secondary interface. However, when SUT has no primary interface or implements multiple primary interfaces tested
    independently, use the actual SUT type for the top-level `sut` variable.
  - _Don't omit the base-class `sut` variable even if it's not applicable to every test_. E.g. tests of overloaded comparison
    operators would need `left` and `right` variables instead of `sut`, however, tests of other methods, like `ToString()`
    and `Equals()` can still use the `sut` variable.
  - _Remove the base-class `sut` variable if it's assigned but never read_. Don't suppress the IDE0052 warning. For types
    dominated by constructors, the base class `sut` variable may never be used by tests and doesn't make sense to keep.

- **Create fields for constructor parameters below the `sut` field** to help the reader understand the constructor
  parameters and their types.
  - _Add a blank line and a `// Constructor parameters` comment above them when non-parameter fields appear below_.
    Omit it when the parameter fields are the only non-`sut` fields in the top-level class.
  - _Add a blank line below the parameter fields_ to separate them from the rest of the test class.
  - _Create parameter fields for the constructor with the most parameters, if SUT constructors are overloaded_.

- **Initialize `sut` inline or in the base class constructor**.
  - _Use the overloaded SUT constructor with the largest number of parameters_.
  - _Don't set SUT properties in the base initializer when SUT has only a default constructor_. Mirror the SUT's actual
    construction: write `new()` and nothing more. Pre-setting properties misrepresents how the SUT is built and makes the
    member tests relying on them more obscure.

- **Keep the base class constructor `private`** - it is still accessible by nested classes but discourages unintended inheritance.
  - _Don't add an empty `private` constructor_. It doesn't add more to what `abstract` already communicates. The nested
    test class pattern is pervasive in this repo, so preventing derived test classes in other files is not worth the 2+ extra
    lines needed for the private constructor.

- **Create a nested test class for every SUT member with observable behavior**.
  Each nested test class serves as an executable description of a specific target member. Nested classes also group multiple
  tests together for maintainability.
  - _Make them `public sealed`_ to communicate that they have concrete tests and aren't meant to be further inherited.
  - _Name them after the target being tested_ - e.g. `Foo.Bar()` should have nested test class `FooTest.Bar`.
  - _Make them inherit from the base test class_ to reuse the test fixture from the base test class.
  - _Place constructor nested test classes first_ to explain how SUT is created.
  - _Order remaining nested test classes alphabetically_ to reduce future merge conflicts.

- **Omit nested test classes for SUT members without observable behavior**.
  - _Don't test code storing parameters in `private` fields for other SUT members_. Private storage logic is verified indirectly
    by the tests of the consuming members.
  - _Do test non-private auto-properties_. A non-private `T { get; set; }` (or `{ get; init; }`) is part of the SUT's observable
    API, even when the getter and setter are compiler-generated. Create a nested test class named after the property with
    at least an `IsSetToGivenValue` test. Indirect coverage through consuming members like `Equals` or `ToString` is not
    a substitute - those tests describe the consumer, not the property.
  - _Don't create a test class for the SUT constructor when it would duplicate tests for other members_. For example, a SUT
    with a constructor storing its argument that could only be observed indirectly by calling `ToString()` could be tested
    with a constructor test, a `ToString` test, or both. Drop the constructor test and keep only the `ToString` test to avoid
    back and forth during reviews.
  - _Do test constructors with hand-coded initialization of accessible fields and auto-properties_. Unlike `ToString()` in
    the example above, whose tests also verify the initialization, fields and compiler-generated auto-properties have no
    hand-coded logic whose tests would also verify it, so the constructor test is required. `IsSetToGivenValue` tests don't
    replace it, because they overwrite the initial value.
  - _Don't add a `Constructor` nested class to verify the compiler-generated default constructor_. Asserting that constructor
    initialized accessible fields and auto-properties to their default/0/null values exercises C# zero-initialization, not
    SUT code. Only add a `Constructor` nested class when SUT with a compiler-generated default constructor has hand-coded
    logic initializing the accessible fields or auto-properties inline.
  - _Don't add a `Default` nested class to verify compiler-generated default-struct state_. Asserting that
    `default(SUT).Member == default(T)` exercises C# zero-initialization, not SUT code. Only add a `Default` nested class
    when the SUT defines executable logic that runs for `default(SUT)` - a property getter, method, or operator with
    hand-written code.

- **Create a separate nested test class for each overloaded SUT member**.
  - Use BCL type names of parameters to disambiguate test class names. SUT with `Task<Stream> OpenAsync(string)`
    and `Task<Stream> OpenAsync(string, int, CancellationToken)` requires two nested test classes - `OpenAsync_String` and
    `OpenAsync_String_Int32_CancellationToken`.
  - Use closed generic type names. Parameter type `Func<Task<int>>` becomes `FuncOfTaskOfInt32` in the class name.
    This makes test classes _closed to modification_ when a new member overloads this parameter to `Func<int>`.
  - Don't create separate classes for parameters with default values. `Task<Stream> OpenAsync(string, int, CancellationToken = default)`
    is a single overload and should still have a single test class.

- **Resolve naming conflicts between SUT members and common methods of test classes**.
  - _Add the `new` keyword to the test sub-class_. When a nested test class is named after a SUT member like `Equals`, `GetHashCode`
    or `ToString`, it conflicts with the method the base test class inherits from `Object`. The `new` keyword resolves the
    conflict: `public new sealed class GetHashCode : FooTest`.
  - _Implement `IDisposable` in test classes explicitly_. If SUT has a `Dispose()` method, the nested test class name `Dispose`
    may conflict with the `Dispose()` method of the test class responsible for test fixture cleanup. Implementing `IDisposable`
    in the test class explicitly resolves the conflict: `void IDisposable.Dispose()`.
  - _Add `_` suffix to nested test class name_. When the test base `Dispose()` method is virtual and overridden by the nested
    test sub-classes, adding the `_` suffix to the test class name resolves the conflict.
    `public sealed class Dispose_ : FooTest`.

- **Use IL names of operator methods, capitalized, for nested test classes**.
  - E.g. `operator <()` (IL name `op_LessThan`) -> `Op_LessThan`.
  - For conversion operators, append `_{ParameterType}_To_{ReturnType}`.
    For example, given `OrdinalString` SUT and its test class `OrdinalStringTest` the
    `explicit operator string(OrdinalString value)` should have nested class `Op_Explicit_OrdinalString_To_String` and
    `implicit operator OrdinalString(string value)` should have nested class `Op_Implicit_String_To_OrdinalString`.

- **Create fields for method parameters in the nested test classes** to help the reader understand the method parameters
  and their types. This guidance also applies to C# indexers.
  - _Add a `// Method parameters` comment above them when non-parameter fields appear below_.
    Omit it when the parameter fields are the only non-`sut` fields in the nested class.
  - _Add a blank line below the parameter fields_ to separate them from the rest of the test class.

- **Use the exact SUT parameter names for fields** even if their names don't meet current naming guidelines.
  - _Don't change SUT parameter names when implementing tests_. Parameter name change is build-breaking for code that specifies
    parameter names explicitly; product versioning restrictions apply.
  - Note: _Test overrides and interface implementations through their declaring APIs_ modifies this rule.

- **Place helpers below tests**.
  Helpers are implementation details of the tests and shouldn't interfere with the tests' purpose of serving as executable
  documentation.
  - _Place helpers applicable to a specific target in the respective nested class_.
  - _Place helpers applicable to multiple targets in their first common base class, below the test classes_.

## Test Methods

- **Test names should accurately describe the expected behavior of the target**.
  - _Describe the SUT's behavior, not the test mechanics_.
  - Say _propagates_ when the caller receives the same exception instance, whether the target doesn't catch it or rethrows
    it with `throw;` or `ExceptionDispatchInfo.Throw`, as described by [CA2200](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2200).
    Mention stack trace preservation only when the test verifies `StackTrace`.

- **Test names should form valid English sentences when read together with the class names**.
  For example `TraceTest.Constructor.ThrowsArgumentNullExceptionWhenTypeIsNull`.
  - _Refer to a SUT parameter by its name_. E.g. for `Equals(object obj)` write `ReturnsFalseWhenObjIsDifferent`. Parameter
    names like `obj` that violate .NET Framework design guidelines should be rare enough that this shouldn't be a readability
    problem.
    Note: _Test overrides and interface implementations through their declaring APIs_ modifies this rule.

- **Each test method should verify a single logical aspect of a single member of the SUT**.
  - Multiple assertions per test method are OK as long as they test the same specific logical aspect of the target.
  - See _Create a separate test for each logical branch_ in the Test Coverage section.
  - Don't separate tests for sequential product code. For example, a constructor storing two parameters in two fields should
    have a single test with two assertions.
  - Inheritance alone is not a separate logical aspect and shouldn't be tested separately.

- **Order test methods so that collapsed definitions form a description of the SUT**, mirroring its doc comments and implementation.
  These rules are for _ordering_ tests, not for _creating_ or _keeping_ them.
  - Place tests describing default/most common behavior first.
    They serve as an executable equivalent of the `<summary>`.
  - Place tests of inputs, edge cases and outputs next.
    They serve as an executable equivalent of the `<param>`, `<exception>` and `<returns>` docs.
  - Place remaining tests in the approximate order of the code they are verifying.
  - Place tests targeting related code/conditions together.

- **Each test method should have clearly visible _arrange_, _act_ and _assert_ sections**.
  - Add comments for multi-paragraph sections where blank lines alone are not sufficient to distinguish them.
    ```csharp
      // Arrange
      ...

      ...

      // Act
      ...

      ...

      // Assert
      ...

      ...
    ```

  - Omit section comments for single-paragraph sections.
    ```csharp
    Bar bar = new("Buz");
    Foo sut = new(bar);

    bool result = sut.TryBar(out Bar actual);

    Assert.True(result);
    Assert.Same(bar, actual);
    ```

  - Omit the blank lines between the single-line sections.
    ```csharp
      Foo sut = new();
      string result = sut.Bar();
      Assert.Equal("Buz", result);
    ```

  - Combine sections in simple, well-factored tests.
    ```csharp
      Assert.True(sut.Equals(other)); // Combined act and assert
    ```

- **Reduce duplication by extracting code into the constructors of the test classes**. This also helps to focus reader on
  the important distinctions between tests.
  ```csharp
  sealed class Foo
  {
      internal int Bar;
      internal int Baz;
      internal bool Equals(Foo other) =>
          Bar == other.Bar && Baz == other.Baz;
  }

  public abstract class FooTest
  {
      readonly Foo sut = new(); // Common arrange logic for all Foo tests.

      static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

      public new sealed class Equals : FooTest
      {
          readonly Foo other;

          public Equals() => // Common arrange logic for all Foo.Equals tests
              other = new Foo { Bar = sut.Bar = fuzzy.Int32(), Baz = sut.Baz = fuzzy.Int32() };

          [Fact]
          public void ReturnsTrueWhenBarAndBazAreSame() =>
              Assert.True(sut.Equals(other));

          [Fact]
          public void ReturnsFalseWhenBarIsDifferent()
          {
              other.Bar = sut.Bar + fuzzy.SByte().Between(1, 5);
              Assert.False(sut.Equals(other));
          }

          [Fact]
          public void ReturnsFalseWhenBazIsDifferent()
          {
              other.Baz = sut.Baz + fuzzy.SByte().Between(1, 5);
              Assert.False(sut.Equals(other));
          }
      }
  }
  ```

- **Test member initialization in the constructor test, not the member tests**.
  Initialization is a logical aspect of the constructor(s). Also, testing it for each member separately creates a significantly
  larger number of tests. On the other hand, setter logic still needs to be tested in the member tests.
  ```csharp
  public sealed class Foo(int bar, int baz)
  {
      public int Bar { get; set; } = bar;
      public int Baz { get; set; } = baz;
  }

  public abstract class FooTest
  {
      readonly Foo sut;

      // Constructor parameters
      readonly int bar = fuzzy.Int32();
      readonly int baz = fuzzy.Int32();

      static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

      FooTest() =>
          sut = new Foo(bar, baz);

      public sealed class Constructor : FooTest
      {
          [Fact]
          public void InitializesProperties()
          {
              Assert.Equal(bar, sut.Bar);
              Assert.Equal(baz, sut.Baz);
          }
      }

      public sealed class Bar : FooTest
      {
          [Fact]
          public void IsSetToGivenValue()
          {
              int expected = sut.Bar + fuzzy.SByte().Between(1, 5);
              sut.Bar = expected;
              Assert.Equal(expected, sut.Bar);
          }
      }

      public sealed class Baz : FooTest
      {
          [Fact]
          public void IsSetToGivenValue()
          {
              int expected = sut.Baz + fuzzy.SByte().Between(1, 5);
              sut.Baz = expected;
              Assert.Equal(expected, sut.Baz);
          }
      }
  }
  ```

- **Generate test inputs**.
  Avoid well-known test inputs the SUT may handle specially or produce on its own, unless the test targets that case.
  - _Use `Fuzzy` to generate test values of common types_. See [fuzzy.instructions.md](fuzzy.instructions.md) for API details.
  - _Define distinct test types instead of reusing existing types to test polymorphic or generic code_. E.g.
    - Instead of reusing `InvalidOperationException` to test exception propagation, create a nested `TestException` class.
    - Instead of reusing `int` and `string` to test collections, create nested `TestKey` and `TestValue` classes or structs.
  - _Use mocks for abstractions_. See [moq.instructions.md](moq.instructions.md) for details.

- **Test argument validation logic** - it's part of SUT's API.
  - _Create explicit tests for missing argument validation_ that can cause `NullReferenceException`, etc. in the consuming
    members of SUT - missing argument validation is a bug in SUT. See the additional rules for more:
    - _Don't test what SUT doesn't do_
    - _Don't create explicit tests for unfixed bugs in callee code_.
  - _Test `null`, empty, etc. as distinct inputs when validation rejects more than one_. A guard like `string.IsNullOrEmpty(x)`
    collapses several conventionally-distinct inputs into a single branch. Test each rejected category with a `[Theory]`,
    e.g. `[Theory, InlineData(null), InlineData("")]` for `IsNullOrEmpty`, plus `InlineData(" ")` for `IsNullOrWhiteSpace`.
    This proves the right guard was invoked and rules out easy common mistakes, such as missing the empty-string check when
    testing only with `null`, or missing the `null` check when testing only with `string.Empty`.

- **Use strongest assertions available**.
  - _Prefer `Assert.Same` over `Assert.Equal`_ when asserting on unique test instances that neither SUT nor test logic replace.
  - _Prefer Collection/String/Span/Etc. `Assert` methods over `Assert.True`_.

- **Minimize cross-member dependencies in assertions**. Each other-member call inside an assertion couples the target's
  tests to that member's correctness; a bug in one target should fail as few unrelated tests as possible.
  - _Pick the most independent member_, e.g. prefer `sut.ToString()` (one dependency) over `(string)sut` that itself calls
    `ToString()` (two dependencies).
  - _Use the same member in different tests_, e.g. even if `operator string()` had an independent implementation too, use
    `ToString` in all tests (same dependency) rather than mixing `(string)sut` and `ToString` (two distinct dependencies).

- **Test exception properties**. Exceptions of common types are expected to provide additional details in properties.
  ```csharp
  var actual = Assert.Throws<ArgumentNullException>(() => action(parameter));
  Assert.Equal(nameof(parameter), actual.ParamName);
  ```

- **Use discard variables when exception properties aren't tested intentionally**.
  This also suppresses the `IDE0058` warnings.
  ```csharp
  _ = Assert.Throws<ObjectDisposedException>(() => action());
  ```

## Test Coverage

- **Strive for 100% test coverage of product code**. This means:
  - Every statement.
  - Every independently reachable logical branch, e.g. `if`, `switch`, `?:`, `?.`, comparison operator, pattern.
    - A `Nullable<T>` comparison is a single branch and doesn't require separate tests for `null` values.
      The following function requires two tests - one for true/equality and one for false/inequality.
      ```csharp
      bool Equals(int? foo, int? bar) => foo == bar;
      ```
  - Every `TargetFramework`.
- **Reject new APIs that don't support this goal**.
- **Improve existing APIs to reach this goal with SemVer restrictions on backward compatibility**.
- **Create a separate test for each logical branch in the body of the target member**.
- **Create tests for existing code even if it's stable and unlikely to change**.
  Tests explain and verify the current implementation, not just that future regressions will be caught.
- **Don't test what SUT doesn't do**.
  - _Don't test the behavior of the target's callees, only that they are called_. The target is tested to verify it delegates
    to the callees and each callee is tested as a separate target. This applies equally to members of other types, e.g.
    `string.Equals()` and other members of the same SUT, e.g. an `Equals(object)` overload calling `Equals(T)`. External
    callees are assumed to be fully tested outside of this repo.
  - _Don't test a contract documented by the target but implemented by a callee_. This is a common trap when a target's
    XML doc re-states a contract implemented by the callee and not the target itself. Don't re-test it through the target.
  - _This intentionally accepts some regressions_. E.g. a future re-implementation that bypasses the callee will
    not be caught by the caller's tests. The tradeoff buys smaller, more independent tests and avoids combinatorial
    duplication between caller and callee test classes.
  - _When possible, invoke callee via an interface or a delegate and verify the target by mocking the callee_. Unlike
    early-bound calls, which cannot be tested directly, late-bound calls can be mocked at run-time and tested directly.
  - _Open-ended "doesn't do X" tests are acceptable only when fixing a specific bug or documenting a key behavior of the SUT_.
- **Cover the target's own branches, not its callees' branches**.
  Branches that live in a callee are covered by the callee's tests. When the target delegates to a callee, the target's
  tests verify that the call happens and that the target's own logic around the call (type-checks, guards, return-value
  handling) is correct — not that the callee's internal branches produce the right answer.
  - _High-cardinality callee outputs require a single test_. E.g. `int GetHashCode()` calling `System.HashCode.Combine()`
    needs a single test because `int` is high-cardinality.
  - _Low-cardinality outputs of callees in other types require a separate test per value_. Use `[Theory]` over the callee's
    output domain, or add one test per distinct forwarded value. E.g. `bool Equals(T)` calling `Equals()` method of its
    member requires two tests because `bool` cardinality is limited to `true` and `false` and asserting only on one of them
    wouldn't be sufficient to prove that the callee was invoked correctly.
  - _Low-cardinality outputs of callees in the same SUT require a single test_. E.g. `Equals(object)` calling `Equals(T)`
    needs a single test forwarding the callee's result. Mishandling the other values is an accepted regression per
    _Don't test what SUT doesn't do_ and re-testing them is duplication per _Minimize duplication_.
  - RE: _Omit nested test classes for SUT members without observable behavior_. Delegation is observable behavior. A delegating
    member needs a nested test class to verify the call and its own branches (type-check, guard, transformation of the return
    value).

## Test Quality

Test failures should be descriptive enough to enable fixing the problem without debugging.
- Each test should fail with expected error.
- Each test should fail independently of other tests.
- Tests should fail and succeed reliably.
- Tests should not fail **or succeed** unexpectedly.

Test quality should be validated to meet the quality bar:
- When a new test is created.
  - Don't assume the product is correct when retrofitting tests. Legacy code may have dead and unnecessary branches.
- When the product code changes in a way that may invalidate the tests.
  - When the product code change is limited to a single member, revalidate all tests for that member.
  - When the product code change spans multiple members of a type, revalidate all tests for the type.

Use _Sabotage_ (a.k.a. mutation testing), the practice of deliberately breaking product code to verify the tests.
Use it both to evaluate individual tests and to find gaps in the test suite.

- **Evaluate a test**: Sabotage the product code in a way that should make the test fail.
  - If the test still passes, it may be incorrect or too loosely specified.
  - If the test fails, verify that it fails with an expected error.
  - If the test always fails with other tests, it may be redundant.
- **Find gaps**: Alter or remove a logical condition, return value, or assignment in the product code.
  - If no test fails, the suite is missing coverage for that behavior.
- **Get empirical evidence**: Actually run the sabotaged product code against the tests to get empirical confirmation of
  your theories. Revert the sabotage after verification.

## Test Design

### [SOLID](https://en.wikipedia.org/wiki/SOLID)
- **Single Responsibility**: Each test verifies one logical condition or behavior.
  See _Create a separate test for each logical branch_.
- **Open/Closed**: The test suite should be open for extension (adding a test for a new property) without modifying
  existing tests.
- **Liskov Substitution**: Test the SUT through its public interface or base type when that's how consumers use it.
- **Interface Segregation**: Don't test unrelated behaviors together just because they share a method.
- **Dependency Inversion**: Mock dependencies to test the SUT in isolation.

### [Simple](https://martinfowler.com/bliki/BeckDesignRules.html)

- **Tests Pass**: Quickly and reliably.
- **Reveals intent**: Each test should accurately represent behavior of the SUT. A test that appears to verify a distinct
  code path but actually exercises the same logic as another test misrepresents what the SUT does.
- **Minimize duplication**:
  - If one test can never fail without another also failing, decide whether it's intended to verify distinct behavior.
  - Make tests more specific when they are intended to verify distinct behavior, so they can fail independently.
  - _Otherwise, remove or combine tests that cannot fail independently_. For equivalent targets and inputs, a test that only
    verifies a non-null result is redundant with one that verifies the result is the expected non-null instance. Remove the
    weaker test.
  - Re-running a callee's input permutations through its caller is duplication, regardless of whether the callee lives in
    another type or in the same SUT. A caller test like `Equals_Object.ReturnsFalseWhenBarIsDifferent` only re-verifies the
    corresponding `Equals_Foo` test. Regressions that only the caller test would catch, e.g. a caller that bypasses the callee
    or mishandles its result, are accepted per _Don't test what SUT doesn't do_ — delete the caller variant and trust the
    callee's tests.
    Exception: distinct argument-validation categories (`null`, empty, whitespace) are covered per _Test argument validation
    logic_.
- **Reduce tests to fewest elements**:
  - Before writing a test for a branch or guard, verify that the branch is reachable independently of the paths already covered.
  - Don't create tests for consistency, parity or structural symmetry. API design principles don't apply to tests.

## Special Cases

- **Test overrides and interface implementations through their declaring APIs**. This helps to explain the SUT better
  in tests and makes tests stronger. Example below is compressed to reduce space and meant to illustrate structure and
  naming, not formatting or comments.
  ```csharp
  public sealed class Foo : IEquatable<Foo>
  {
     public override bool Equals(object foo);
     public bool Equals(Foo foo);
  }
  public abstract class FooTest
  {
      readonly Foo sut = new();
      public sealed class Equals_Foo : FooTest
      {
          new readonly IEquatable<Foo> sut; // tests implementation of IEquatable<T>.Equals(T? other)
          readonly Foo other = new(); // field and test method names are consistent with IEquatable<T>
          public Equals_Foo() => sut = base.sut;
          [Fact] public void ReturnsFalseWhenOtherIsDifferent() => Assert.False(sut.Equals(other));
      }
      public sealed class Equals_Object : FooTest
      {
          new readonly object sut; // tests override of Object.Equals(object obj)
          readonly object obj = new(); // field and test method names are consistent with Object
          public Equals_Object() => sut = base.sut;
          [Fact] public void ReturnsFalseWhenObjIsDifferent() => Assert.False(sut.Equals(obj));
      }
  }
  ```
  In the context of rules _Use the exact SUT parameter names for fields_ and _Refer to a SUT parameter by its name_,
  this means that SUT is narrowed to implementation of a specific API, so that API's parameter names apply.

- **Use `[WindowsOnly("reason")]` from `TestFramework` to skip tests that can't run on Linux**.
- **Use `[Fact(Explicit = true)] // TODO: {Reason}` to exclude intentionally failing and flaky tests**.
  Explicit tests are discovered but not run by default. Run them with `--explicit on`, e.g.
  `dotnet test -- --filter-method "FullyQualifiedMethodName" --explicit on`.
  - **Create explicit tests to demonstrate unfixed SUT bugs**.
    - `{Reason}` should be `SUT bug. {brief explanation}`.
    - Name the test for the expected post-fix behavior (e.g. `IsSymmetric`), not for the bug.
    - The _Don't test what SUT doesn't do_ rule doesn't apply to bugs caused by code missing from SUT.
  - **Don't create explicit tests for unfixed bugs in callee code** - the _Don't test what SUT doesn't do_ rule still applies
    to callee bugs. E.g. SUT passing an argument to a callee that dereferences it without a `null` guard, is a bug in the
    callee. It's out of scope for SUT and should have an explicit test in the callee's test suite instead.
    When the callee is outside of this repo, report the bug to the user and ask them to submit an issue to the callee's repo.
  - **Create explicit tests impossible to implement due to SUT testability limitations**.
    - Include `// TODO: SUT testability limitation. {brief explanation}`.
    - Have them `throw new NotImplementedException()`.
    - RE: _Each test method should verify a single logical aspect of a single member of the SUT_. The name of the not-implemented
      explicit test explains what needs to be tested (a single logical aspect of a single member), while the TODO comment explains
      why it is not currently possible.
  - **Make flaky tests explicit to unblock CI**. Include `// TODO: Flaky test. {brief explanation}`.
  - **Keep the `// TODO: {Reason}` short and single-line**.
    It will be displayed in the _Task List_ in _Visual Studio_.
  - **Include detailed explanation of the exclusion in the body of the test method**.
    - Add comments to explain _why_ the test code demonstrates a problem.
    - Use `Assert` messages, when available.
  - **When fixing a problem documented by an explicit test, remove `Explicit = true` and the `// TODO` comment**, along with
    the comments explaining why the test code demonstrates the problem, implement the test if it throws `NotImplementedException`,
    then verify that it passes.
  - **Remove explicit tests when their `{Reason}` becomes invalid without fixing the problem**, such as when missing logic
    previously documented as a bug is now unnecessary based on the latest `*.instructions.md` or docs.
- **Consider fixing problems documented by explicit tests when changing SUT**.
  Any change in SUT should evaluate fixing the problems already documented by the existing tests.
- **Don't fix problems documented by explicit tests when putting SUT under test**.
  Combining SUT changes with the initial implementation of the test suite increases the risk of breaking it without adequate
  test coverage.
- **Don't use `Assert.IsAssignableFrom<T>()` or `Assert.IsType<T>()`**. On their own, these are unnecessary.
  - SUT inheritance can be verified by compiler and shouldn't be tested separately.
  - When type information cannot be verified by compiler, additional asserts are needed on state or behavior of the expected
    type. Use type casting instead.
- **Don't use `Assert.NotNull()`**. On its own, it's unnecessary.
  - Without additional asserts on the state or behavior of the instance that shouldn't be null, `Assert.NotNull()` is insufficient.
  - Let additional asserts accessing the instance that shouldn't be null throw `NullReferenceException` if it is.
- **Use `ITestContext.CancellationToken` when a pass-through `CancellationToken` parameter is needed**.
  xUnit provides a unique `CancellationToken` in `TestContext.Current`. When the target doesn't implement its own cancellation
  logic and simply passes the `CancellationToken` to dependencies, using the xUnit-provided token eliminates the need to
  create and dispose a `CancellationTokenSource`.
  ```csharp
  using Xunit;
  readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;
  await sut.CloseAsync(cancellationToken);
  ```

## Integration Tests

- **Avoid integration tests**.
  Integration tests typically don't have the 1:1 equivalency with the product code and tend to drift away over time.
  They are also often fragile, producing flaky results and difficult to modify over time. Instead of integration tests,
  strive to unit test each product type in isolation of its dependencies.

- **Don't hard-code/commit test files, generate them on-the-fly in tests**
  - Generate test file contents with appropriate `Fuzzy` values.
  - Replace existing hard-coded/committed test files with file-generation logic.

- **Generate unique paths for test files, if possible**.
  When SUT accepts file names or paths, generate unique file or directory names to avoid sharing test fixture between tests.
  This is not possible when SUT expects files or paths with well-known names.

- **Delete unique test files and directories at the end of each test** to avoid disk usage bloat.

- **Delete well-known files and directories at the start and the end of each test** to reduce the risk of tests failing
  when reusing the test fixture left over by previous tests.

- **Prefer test class constructor and `IDisposable.Dispose` for file cleanup over `try/finally` in individual tests**
