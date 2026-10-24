# Read-tolerant versioning

## Status

Accepted

## Context

Data formats evolve over time. Strictly rejecting every newer version can
discard useful data even when a reader understands all fields it encounters.
Producers should not need separate output modes for every consumer version.

## Decision

- Writers emit the newest format they implement; they do not downgrade output
  for older readers.
- Readers interpret data using the newest rules they understand.
- Readers ignore unknown additive fields and preserve data they do understand.
- Missing optional data is not corruption. Malformed values and invalid
  references fail clearly.

## Consequences

- Producers avoid per-version downgrade paths.
- Readers remain useful as formats gain optional fields.
- Breaking changes still require an explicit compatibility boundary.
- Each reader must distinguish unknown additions from malformed known data.

## Alternatives considered

- **Reject every version mismatch:** rejected because a reader may understand
  all data present in a newer document.
- **Versioned writers:** rejected because they require a downgrade path for
  every supported target.

## References

See the GIM JSON reader documentation for current dispatch behavior.
