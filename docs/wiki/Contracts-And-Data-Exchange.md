# Contracts and Data Exchange

## The rule

| Rule | Statement |
| -- | -- |
| **1** | A value is a type, not a primitive. An email address is `EmailAddress`, not `string`. |
| **2** | Two doors. `Create` validates and can fail; `Hydrate` trusts and cannot. |
| **3** | Normalise, then validate — in that order, in one place. |
| **4** | Expected outcomes are values. Exceptional circumstances throw. |
| **5** | The schema is the single source of truth for a wire shape. Everything else is derived. |

## Why

**Rule 1** exists because a primitive is an unchecked assumption. `string email` is a promise the compiler
cannot verify, so every consumer re-verifies it or hopes. A type makes the promise checkable once and
carries its own rules to every place it appears — including places the original author never saw.

**Rule 2** exists because validation has a direction. Inbound data must be checked; data you produced
yourself must not be re-checked, because re-checking a value that is already known-good turns a data-quality
problem into a crash in an unrelated place. Two doors with different contracts make the choice explicit and
reviewable: `Create` at the boundary, `Hydrate` everywhere past it.

**Rule 3** exists because the order is not arbitrary. `" Foo@Bar.com "` and `"foo@bar.com"` are the same
address, and a validator that rejects the first is wrong. Normalising inside the factory means there is
exactly one normalised form, so equality, hashing, dictionary keys and database lookups all agree.

**Rule 4** exists because an exception is a statement that the caller did not expect this. "This email is
taken" is expected; it is a branch, not a fault. Modelling it as a value keeps the compiler's help — the
caller must handle it — without the cost of exceptions as control flow. Faults still throw, and that
distinction is the whole point.

**Rule 5** exists because a wire shape described twice is a wire shape that disagrees with itself. The
schema is the contract; the C# type, the JSON binding, and the documentation are all derived from it.

## How they compose

```text
inbound
  request body  ──schema──▶  validated shape  ──Create──▶  domain value
                                                              │
                                                              ▼
                                                       domain logic
                                                              │
                            Result<TValue, TError> ◀──────────┘
                                     │
                                     ├── success ──▶ TValue
                                     └── failure ──▶ TError  ──map──▶ response
```

Four conversions, each with one owner:

- **Schema → validated shape.** The schema decides what a well-formed payload is, before anything is
  constructed.
- **Validated shape → domain value.** `Create` is the only door that validates; it runs normalisation and
  the type's own invariants.
- **Domain value → result.** Domain logic returns `Result<TValue, TError>` rather than throwing for
  expected failure.
- **Result → response.** A mapping layer turns a `TError` into a status code and a body. Nothing above it
  knows what an HTTP status is.

## Rule 1 and 2 in practice

[value-objects](https://github.com/purview-dev/value-objects) is the reference implementation, and it is
source-generated rather than hand-written: mark a `partial` record struct with `[Scalar]` and the generator
emits the factories, the equality, the comparison, the conversions and the JSON converter.

```csharp
[Scalar]
public readonly partial record struct EmailAddress
{
	public string Value { get; }

	static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

	static partial void OnValidate(ref string value)
	{
		if (!value.Contains('@', StringComparison.Ordinal))
		{
			throw new ArgumentException($"'{value}' is not an email address.", nameof(value));
		}
	}
}
```

Note what the two hooks encode: `OnNormalize` runs first and makes the value canonical, `OnValidate` runs
second and can reject it. The generator supplies `Create`, `Hydrate` and `TryCreate` around them, plus
`Empty`, `CompareTo`, the underlying value's formatting and parsing interfaces, and a JSON converter that
serialises the scalar as its underlying value rather than as an object. The `Value` property stays visible,
so a value object is never a black box that has to be unwrapped through a helper.

The same generator covers the storage boundary: reference `Microsoft.EntityFrameworkCore` and it emits the
value converters, comparers and complex-type mapping, plus a `ConfigureValueObjects` extension — so queries
use the value object type directly instead of `.Value`. That is the mapping EF Core should configure once,
and it is why a value-object key also needs a converter-aware value generator when the application owns the
value. See [EF Core Integration](EF-Core-Integration.md).

## Rule 4 in practice

[results](https://github.com/purview-dev/results) supplies the type and the ergonomics:

```csharp
Result<Tenant, TenantError> GetTenant(TenantId tenantId) =>
	_tenants.TryGet(tenantId, out var tenant)
		? Result<Tenant, TenantError>.Success(tenant)
		: new TenantNotFound(tenantId).AsFailure<Tenant>();
```

The error side is a C# union, so the set of failures is closed and the compiler knows it:

```csharp
[GenerateResult]
public readonly union TenantError(TenantNotFound, TenantDisabled, TenantAlreadyExists);
```

`AsFailure<TValue>()` is generated per case by the source generator bundled in `Purview.Results`, which is
what keeps the call site free of a `new TenantNotFound(...)` cast to the union type. The union receiver gets
`Union.Failure(...)` and `Union.Success(...)` factories, so constructing a result never requires naming the
union's underlying representation.

The sentence to keep in mind is the one in the package's own README: *"Exceptional circumstances still
throw; expected outcomes are values."* A missing tenant is expected. A database that is unreachable is not,
and it should surface as an exception rather than as a `TenantError` case, because a caller who is forced to
handle it as a value will handle it wrongly.

## Rule 5 in practice

[zodsharp](https://github.com/purview-dev/zodsharp) is the schema layer: a schema validation library for C#
ported from TypeScript's Zod, with struct-based rules, a fluent API, a `[ZodSchema]` source generator, and
DataAnnotations support.

The important property is not the performance — it is that one schema serves several jobs:

| Consumer | What it derives from the schema |
| -- | -- |
| Inbound validation | The rules, with zero-allocation checking |
| JSON Schema export | A machine-readable contract for consumers and documentation |
| JSON Schema import | A schema built from an existing contract, so the C# side can adopt one that already exists |
| ASP.NET Core integration | Model validation wired to the framework's own pipeline |
| Result mapping | `ValidationResult<T>` bridged into `Result<TValue, TError>`, and failures mapped onto `ProblemDetails` |

The bridges are separate packages rather than one package with everything in it — `Purview.Results.ZodSharp`
and `Purview.Results.ZodSharp.AspNetCore` — so a consumer that does not need the mapping does not take the
dependency.

## Validation results are values too

A validation outcome is modelled, not thrown:

```csharp
validator.ValidateAsync(Any(), Any()).Returns(ValidationResult<TestAggregate>.Success(aggregate));
validator.Validate(Any()).Returns(
	ValidationResult<TestAggregate>.Failure(
		ValidationError.Create("any", "error", [nameof(TestAggregate.Name)])));
```

`IsValid`, `Failures`, and each failure's `PropertyName` are the read surface, so a caller can map failures
onto a response without parsing a message. That is what makes the boundary conversion mechanical rather than
clever.

## How it is enforced

| Rule | Enforced by |
| -- | -- |
| A value is a type | Review — and, once the package is referenced, the source generator's diagnostics |
| `Create` and `Hydrate` are the only doors | The generator emits them; a hand-written constructor is a review failure |
| Normalise before validate | The generator runs the hooks in that order |
| Expected failures are values | Review — a thrown exception for an expected outcome is visible in review |
| The schema is the source of truth | Review, plus the export/import tooling that makes a second definition unnecessary |
| JSON serialisation of a scalar value | The generator's emitted converter; a test asserting the wire shape is the check |

The one that most benefits from a test is the wire shape: a value object's JSON representation is a contract
with something outside the repository, so it belongs in a test that asserts the exact payload rather than in
a hope.

## At your scale

- **One project.** Adopt rule 1 for the identifiers and the values you already validate by hand. Add rule 4
  for the one failure mode your callers currently catch.
- **A service with a boundary.** Adopt all five. Rules 2 and 3 pay for themselves the first time an inbound
  payload arrives with different casing and a lookup fails for a value that is already in the database.
- **A published contract.** Export the schema and check it in. A consumer-facing wire shape that only exists
  as C# code is a wire shape nobody outside can test against.
- **Several services.** Version the schema, not the code. The schema is the thing both sides agreed on, and
  it is the thing to keep compatible.

## See also

- [Worked Examples](Worked-Examples.md)
- [EF Core Integration](EF-Core-Integration.md) — value converters and value-object keys in the model
- [Well-Known Identifiers](Well-Known-Identifiers.md) — value objects that carry a derived identity
- [Testing and Test Data](Testing-And-Test-Data.md)
- [value-objects](https://github.com/purview-dev/value-objects)
- [results](https://github.com/purview-dev/results)
- [zodsharp](https://github.com/purview-dev/zodsharp)
