# Design Philosophy Notes (Session Memory)

## Core Philosophy Extracted from User Feedback

### 1. **Systems Tool, Not Application**
This is a **debugger/introspection tool**, not an end-user application. Design decisions should prioritize:
- **Completeness over convenience**: Never truncate, limit, or heuristically filter data
- **Transparency over safety**: Show everything (public + internal members), don't protect the user from "too much information"
- **Correctness over performance**: Get it working correctly first, optimize later only if demonstrably needed

**Anti-pattern to avoid:** Adding artificial limits (e.g., "max 1000 elements") because you're worried about resources.

### 2. **No Defensive Programming for Hypothetical Issues**
- Don't add complexity to handle edge cases that haven't been observed
- Don't implement paging/truncation/limits preemptively
- Don't worry about latency until it's measured as a problem
- Don't add safeguards against "fetching too much data"

**Rationale:** The machine has ample resources. If an OS truncated `ps` output to 100 processes, you couldn't debug. Same principle applies here.

### 3. **Full Information is Non-Negotiable**
When a client requests data:
- Return **all** of what was requested
- If it's a collection, return the entire collection (unless client explicitly requests a range)
- If it's an object, return all public + internal members
- No server-side filtering/limiting without explicit client request

**Collection handling approach:**
- If concerned about size, return an opaque collection handle with a size/count
- Client can then request explicit ranges (e.g., indices 0-999)
- If collection mutates between requests, that's expected behavior (return actual current state)

### 4. **Simple Choices for Implementation**
When faced with design decisions:
- Make choices that simplify implementation while maintaining extensibility
- Don't over-design for features not yet needed (e.g., multiple viewer composition before basic inspection works)
- Defer optional features until there's demonstrated need

### 5. **Multi-Client is a Hard Requirement**
Both human (web UI) and agent (MCP) operation must work from day one. This isn't a prototype that evolves—it's an integrated system.

### 6. **Server is Minimal/Dumb, Clients are Smart**
- Server provides primitive operations (inspect, enumerate)
- Server doesn't make decisions about what's "too much" or "too complex"
- Clients handle filtering, prettification, schema application, navigation logic
- Server doesn't decide when to clear handles (clients do via explicit API)

### 7. **Inspection Depth**
- **Visibility:** Public + internal members (not private)
- **Recursion:** One level at a time (return handles for referenced objects)
- **Collections:** Entire collection in response (no lazy loading unless explicitly requested via range API)

### 8. **Performance Philosophy**
- "High latency is acceptable BUT it shouldn't be high if we implement this well"
- Program efficiently in general with room to iterate and debug
- Don't program defensively for latency
- Profile and optimize only when measured as necessary

### 9. **Threading**
- Game is multi-threaded
- Unity APIs don't necessarily require main thread (context-dependent)
- Reflection can happen off-thread as long as results are marshaled correctly
- Use async/threading where it makes sense, but start simple and optimize

### 10. **Technology Choices**
- Use whatever works; inspect existing game assemblies to determine what's available
- Happy to bundle dependencies if needed
- Look at existing extractor mod for deployment model reference

## Questions to Stop Asking

1. ❌ "Should we limit collection size to N elements?"
   - **Never add artificial limits**

2. ❌ "Is latency acceptable if it's X milliseconds?"
   - **Implement efficiently, measure later**

3. ❌ "Should we truncate/filter to protect the user?"
   - **No defensive filtering**

4. ❌ "Can we defer multi-client to v2?"
   - **Multi-client is v1 requirement**

5. ❌ "Should we add paging for large datasets?"
   - **Return everything requested; add range API if size is concern**

6. ❌ "Is this feature necessary or can we simplify by removing it?"
   - **Valid question, but apply user's philosophy: simple implementation, full functionality**

## Questions to Ask Instead

1. ✅ "What's the simplest implementation that provides complete functionality?"
2. ✅ "Does this design allow us to add X later without major refactoring?"
3. ✅ "Are we adding complexity to solve a problem we haven't observed?"
4. ✅ "Does this align with 'systems tool' philosophy (complete, transparent, correct)?"
5. ✅ "What do the existing game assemblies provide that we can leverage?"

## Implementation Priority

Based on user philosophy:
1. **Get inspection working correctly** (server + both clients)
2. **Iterate on usability** (once proven functional)
3. **Add schema/viewer system** (once inspection is solid)
4. **Optimize** (only when measured bottlenecks exist)

Do not skip step 1 to prematurely optimize or add features.
