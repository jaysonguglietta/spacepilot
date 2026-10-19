# Historical audit evidence

The reports and fixtures in `2026-10-16/` review SpacePilot at commit
`943a196a12a879a0ca2d9b9e97985fe33b3a5697`. Their line references and
vulnerability status apply to that commit. The fixes are tracked in
[PR #1](https://github.com/jaysonguglietta/spacepilot/pull/1); consult the
current code and CI results before treating a finding as open or closed.

The proof scripts are historical regression evidence. They build against the
audited version and may fail against later fixes. They create disposable
fixtures under the system temporary directory; do not run them against real
user data. They are not part of current CI.
