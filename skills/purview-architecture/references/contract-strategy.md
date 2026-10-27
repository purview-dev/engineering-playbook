# Contract strategy: Contracts, OpenAPI or Hybrid

When a solution has an HTTP API and one or more clients, something has to define the shapes that cross
between them. There are three ways to do it, and the wrong one fails in one of two directions:

- **Rules drift.** The client holds a second, weaker copy of the domain rules, and a request the UI
  accepted is rejected by the server — or a break only surfaces at runtime.
- **Decoupling bought in advance.** A .NET client in the same solution regenerates its models from
  the API's own OpenAPI document, and throws away every value object and rule to buy an independence
  the solution does not have.

The strategy is chosen from the clients that **exist today**. A planned client is noted; it does not
count. "Later" and "in case" do not justify a strategy any more than they justify a project — see
`proportional-architecture.md`.

## The three strategies

### Contracts

A .NET class library holding the request and response types, built on the domain's value objects and
carrying `[ZodSchema]`. The server references it, and so does every .NET client — including a Blazor
WebAssembly client.

There is one copy of every rule, error code and normalisation, and both sides run the same generated
validators.

### OpenAPI

The server publishes an OpenAPI document, and clients generate their models and calls from it. It is
language-neutral and versions independently.

Generated models are primitives. Value objects, custom ZodSharp rules, error codes, and trimming and
casing are lost; only what JSON Schema can express survives.

### Hybrid

Contracts is the **single source of truth**. The OpenAPI document is **generated from it**, with the
component schemas exported by ZodSharp (`{T}Schema.BuildJsonSchema()`), so the document and the
validators cannot drift.

.NET clients reference Contracts. Non-.NET clients generate from the document and take their
validation rules from the exported JSON Schema — for TypeScript, as Zod schemas; the
`purview-zodsharp-json` skill covers the export.

## Decision procedure

1. **List the clients that exist today.** For each, record:
   - its language and runtime;
   - who owns it;
   - whether it lives in the same repository and solution;
   - whether it ships **in lockstep** with the server or on its own cadence.

   A planned client is noted, but it does not count yet.
2. **Every client is .NET, in the same solution, and ships in lockstep → Contracts.** This prevents:
   - rules drifting between client and server;
   - a second, weaker copy of the domain rules in the UI;
   - contract breaks that only surface at runtime.

   Publish OpenAPI anyway if documentation or tooling wants it, but generate it from Contracts. It is
   an output, never an input.
3. **At least one client is not .NET, or ships independently → Hybrid.** This prevents:
   - non-.NET clients hand-writing shapes and rules;
   - the .NET clients losing their value objects and rules to a generated client.

   It requires the independent-versioning rules under [Version skew](#version-skew).
4. **No .NET client exists, or the API is a surface whose clients the team does not own (public or
   partner) → OpenAPI**, still generated from the server's types. A separate Contracts project is not
   earned when the server is its only consumer.
5. **A .NET client in another repository with its own release cadence** prefers Contracts shipped as
   a package, if that team accepts the dependency. Otherwise treat it as step 3.

```text
flowchart TD
    A["List the clients that exist today"] --> C{"Every client .NET,<br/>same solution, lockstep?"}
    C -- yes --> K["Contracts<br/>OpenAPI only as an output"]
    C -- no --> B{"Any .NET client<br/>at all?"}
    B -- no --> O["OpenAPI<br/>generated from the server's types"]
    B -- yes --> D{"The only other clients are .NET<br/>in another repository,<br/>and they accept a package?"}
    D -- yes --> P["Contracts, shipped as a package"]
    D -- no --> H["Hybrid<br/>Contracts is the source, OpenAPI generated from it"]
```

Steps 3 and 4 do not overlap: a .NET client in the solution alongside a partner or non-.NET client is
step 3, because the .NET client still has value objects and rules to lose.

## Rules that follow

- **Never regenerate a C# client from your own API's OpenAPI document when that client is in the same
  solution.** It throws away value objects and rules to buy decoupling the solution does not have.
- **Never hand-maintain a parallel set of models** on either side. Two definitions of one wire shape
  disagree eventually, and nothing fails when they start to.
- **The server always validates.** Sharing validators with a client is for user experience and
  consistency. It is never a substitute for validating on the server, because the client is untrusted.
- **Moving from Contracts to Hybrid must be additive.** Because Contracts is already the source, adding
  a non-.NET client means turning on generation of the OpenAPI document and a contract test. Nothing in
  Contracts changes. Design for that, but do not build it until the client exists.

## Contracts project constraints

- **References only the domain library (value objects) and the Purview packages.** Never the server,
  persistence, HTTP clients or hosting. A Contracts project that references the server is a server
  that every client now ships.
- **Browser-safe when a WebAssembly client references it.** No file, network or process I/O, safe to
  trim, and no reflection-dependent behaviour. The same applies transitively to the domain library,
  because the client downloads it too. Enforce it with an architecture test — forbidden packages and
  forbidden framework references on `Contracts` and `Core` — not by review; see
  `purview-architecture-testing`.
- **Deserialisation mode is a deliberate choice per direction.** The server reads requests through
  `Create` (strict), because the client is untrusted. Record what the client uses for responses, and
  why — strict catches a server that has drifted, and `Hydrate` trusts a server the client cannot
  verify.

## Version skew

| Shipping | Strategy | Failure it prevents | What to do |
| -- | -- | -- | -- |
| Lockstep | Contracts, internal apps | A silent failure that looks like a bug, when an old cached client talks to a new server | The server stamps its build on responses; the client prompts the user to reload when the stamps differ |
| Independent | Hybrid, OpenAPI | A deployed client breaking without warning | Additive changes only; a breaking change gets a new route version; a contract test compares the generated document against the last published one and fails on a breaking difference |

## Backend-for-Frontend

A BFF is a host, not a layer, and it is held to the same justification test as anything else.

- **It is earned by keeping tokens out of the browser.** It signs the user in, holds the tokens, and
  gives the browser a cookie. It forwards calls with the **user's** access token, never its own
  identity, so the API's authorisation and audit trail stay per-user. This prevents tokens being stolen
  from browser storage, and every action being recorded as the BFF's.
- **A BFF that only forwards calls does not reference Contracts** and never reads payloads. Reading a
  payload it does not own makes it a second consumer that has to change in lockstep for no benefit.
- **A BFF that combines several API calls into one response owns its own contract** for those
  responses, chosen with this same procedure. That is a separate justification. The forwarding-only
  test — "a service layer that forwards" is not earned — still applies to everything else it does.
- **One BFF per client kind, not one per screen.** Web and mobile may each get one when their sign-in
  or response needs differ.

The policy names it `Bff`; its dependency row is in `project-responsibilities.md`.

## Worked examples

| Clients today | Strategy |
| -- | -- |
| Blazor WebAssembly app, same solution, lockstep, behind a BFF | Contracts. The BFF forwards only. OpenAPI generated for documentation. |
| Blazor WebAssembly app plus a React Native mobile app on its own release cadence | Hybrid. Mobile generates from the document and takes Zod rules from the exported schema. Additive-only versioning. A mobile BFF if sign-in differs. |
| Partner integrations only | OpenAPI, generated from the server's types. No Contracts project. |

## See also

- [Contracts and Data Exchange](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Contracts-And-Data-Exchange.md)
  — `Create` and `Hydrate`, and the schema as the source of truth for a wire shape
- [zodsharp](https://github.com/purview-dev/zodsharp) — authoritative on `[ZodSchema]` and JSON Schema
  export
