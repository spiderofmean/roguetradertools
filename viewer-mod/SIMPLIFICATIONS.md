# Simplifications Applied

This document summarizes the unnecessary complexity that was removed from the codebase.

## Philosophy

**The system is a debugging tool, not production software.** Defensive patterns that hide failures are counterproductive. Configuration that doesn't change behavior adds complexity without value.

## Changes Made

### 1. Removed Debug/Release Configuration
**Before:** Build and deploy scripts accepted `-Configuration Release|Debug` parameter  
**After:** Always builds Release mode

**Rationale:**
- Debug vs Release doesn't change program behavior for this tool
- No debugger is attached to the game process
- Release mode is always what you want
- Parameter was pure ceremony

**Files Changed:**
- `scripts/build.ps1` - Removed parameter, always uses Release
- `scripts/deploy.ps1` - Removed parameter, always uses Release

### 2. Replaced Heuristic Game Location with Required Configuration
**Before:** Deploy script tried multiple Steam paths with fallback  
**After:** Requires `config.json` with explicit `ModsFolder` path

**Rationale:**
- Game location is static per machine - doesn't change between runs
- Heuristics fail silently or guess wrong
- Explicit configuration makes errors clear
- Failing fast with clear error > silent fallback behavior

**Files Changed:**
- `scripts/deploy.ps1` - Now requires `config.json`
- `config.json` - New required configuration file (gitignored)
- `config.example.json` - Example for users to copy
- `.gitignore` - Ignores local `config.json`

### 3. Removed Optional test-config.json
**Before:** Tests could optionally load `scripts/test-config.json` to override base URL  
**After:** Tests hardcode `http://localhost:5000`

**Rationale:**
- The mod always runs on localhost:5000
- Configuration that never changes is not configuration
- Optional config files that are never used add cognitive load
- If someone needs a different port, they can edit the constant

**Files Changed:**
- `scripts/test-02-connectivity.js` - Removed config file loading
- `scripts/test-03-integration.js` - Removed config file loading
- `spec/TEST_PLAN.md` - Removed Configuration section
- `.gitignore` - Removed `scripts/test-config.json` entry

### 4. Removed Defensive Error Handling
**Before:** Multiple try/catch wrappers that only logged exceptions  
**After:** Let exceptions propagate naturally

**Rationale:**
- Catching and logging errors hides failures
- Tools should fail fast and loud
- Exception stack traces are more useful than logged messages
- Unity will log unhandled exceptions anyway

**Files Changed:**
- `mod/src/Entry.cs`:
  - Removed `_started` guard (unnecessary)
  - Removed try/catch wrapper (hides initialization failures)
  
- `mod/src/ViewerBehaviour.cs`:
  - Removed try/catch in `Start()` (hides server start failures)
  - Removed try/catch in `Update()` (hides queued action failures)
  - Removed try/catch in `OnDestroy()` (hides cleanup failures)
  - Simplified `ExecuteOnMainThread` exception wrapping

- `mod/src/State/HandleRegistry.cs`:
  - Removed `ArgumentNullException` check in `Register()` (let null fail naturally)

### 5. PowerShell Compatibility Fix
**Issue:** Used `Join-Path $a $b $c` syntax (PowerShell 7+ only)  
**Fix:** Nested `Join-Path` calls for PowerShell 5.1 compatibility

**Files Changed:**
- `scripts/build.ps1`
- `scripts/deploy.ps1`

## What Remains

The following complexity is **intentional and necessary**:

### Main Thread Marshalling
The `ExecuteOnMainThread` mechanism is complex but required - Unity APIs can only be called from the main thread, and HTTP requests come from background threads.

### Null Checks in Core Logic
Null checks in `ObjectInspector`, `RootProvider`, and `ImageExtractor` guard against bad game state, not programming errors. These are appropriate.

### Collection Type Detection
Reflection logic for identifying and iterating collections is inherently complex due to .NET's type system.

### Handle Registry Race Condition Handling
The double-check in `Register()` handles threading race conditions correctly. This is not defensive programming, it's correct concurrent code.

## Testing

After simplifications:
- ✅ Build script works: `.\scripts\build.ps1`
- ✅ Deploy script validates config properly
- ✅ All test scripts still function
- ✅ Code compiles with no errors

## Impact

**Lines of Code Removed:** ~100  
**Configuration Complexity Removed:** 2 optional config files, 1 parameter  
**Failure Hiding Removed:** 5 try/catch blocks

**Result:** Clearer code, explicit failures, less cognitive load.
