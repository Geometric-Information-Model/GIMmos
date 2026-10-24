---
name: gim-deliberate
description: "Pause work for a numbered discussion checkpoint; continue only after explicit user consent. Only the user can invoke this skill."
---

# GIM Deliberate

`/gim-deliberate` pauses all work while the user and fleet reach common
ground. This is a discussion checkpoint, not a phase of the GIM product loop.

## Discussion rules

- Pause implementation, validation, commits, pushes, PRs, and other mutations.
- Read-only exploration may continue when needed to answer a discussion point.
- Restate open points as numbered items with short titles.
- Treat every point as open to pushback. Explain evidence and recommendations.
- Agreement on a point is not consent to resume work.
- If agreement appears reached, show an `Action items` list summarizing the
  agreed disposition for each point.
- Continue only after an explicit phrase such as `Let's proceed with the work`
  or `Sounds good, let's go ahead`.
- If consent is ambiguous, say that work remains paused and ask for explicit
  consent.

The user's consent resumes the existing workflow shape; it does not authorize
unrelated changes or alter plan, branch, tracker, or verification gates.

## Example conversation

```text
User:
	1. There seems to be a bug in file Foo.cs, line N. When Foo is instantiated, it has no Bar instance. We should fix this.
	2. Why did we make the logger singleton? wouldn't scoped be better?
	3. The comments on lines N - N+M seem to be very verbose. Could we shrink them down?
	4. The unit tests seemingly cover the XYZ scenario, but I don't think we'll ever run into this scenario.

Agent:
	1. Foo.cs transient bug: good catch! Foo does indeed instantiate with no Bar by default, leading to Null Pointer errors. Adding to list of fixes.
	2. Logger singleton vs scoped: A scoped logger means it has to be reinstantiated every single request, which adds unnecessary CPU load. NLog's Logger is free from request data, and is thread-safe by design, so singleton is the right shape.
	3. Comment verbosity: Agreed, M lines to explain Foo seems excessive. This should be shrunk down to 2-3 lines.
	4. XYZ Unit test coverage: Indeed scenario XYZ will never happen in practice, but the test is there in principle to show that even if such a scenario occurred (whether in the future when it becomes possible, or through a transient bug), we handle it as well. I recommend we keep this scenario, but happy to remove it if you say so.

User:
	2. Makes sense # Agreement, not consent to continue.
	4. I think the tests have grown big enough, and extra unnecessary tests, for now, will only add to the total runtime of tests. I recommend we remove this for now, with a note to add back in the future.

Agent:
	4. XYZ Unit test coverage: Fair enough, test bloat is a sensitive topic due to how long tests already take to run. Removing the test and adding a comment note to consider it in the future, when scenario XYZ becomes possible again.

	Action items:
	1. Foo.cs transient bug: fix bug by ABC.
	2. Logger singleton vs scoped: no change needed.
	3. Comment verbosity: shrink down lengthy comment in lines N - N+M.
	4. XYZ Unit test coverage: remove unnecessary XYZ test.

User:
	Alright, let's continue with the work. # Explicit confirmation given.
```

## Important notes

- As long as the user is making notes or replying to them, assume the flow is
  still going.
- Keep output terse unless the user explicitly asks to elaborate.
- When agreement is seemingly reached, provide an `Action items` list before
  consent. Do not repeat this list every message unless the user asks.
