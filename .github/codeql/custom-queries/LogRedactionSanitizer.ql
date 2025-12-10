/**
 * @name Custom sanitizers for log injection
 * @description Defines custom sanitization methods for CodeQL analysis
 * @kind problem
 * @problem.severity warning
 * @id custom/log-sanitizers
 */

import csharp
import semmle.code.csharp.security.dataflow.LogForgingQuery
import DataFlow::PathGraph

/**
 * A sanitizer for log injection that recognizes LogRedaction methods
 */
class LogRedactionSanitizer extends DataFlow::BarrierGuard {
  LogRedactionSanitizer() {
    this.asExpr() instanceof MethodCall and
    exists(MethodCall mc |
      mc = this.asExpr() and
      mc.getTarget().getDeclaringType().hasQualifiedName("Listenarr.Api.Services", "LogRedaction") and
      (
        mc.getTarget().getName() = "SanitizeText" or
        mc.getTarget().getName() = "SanitizeUrl" or
        mc.getTarget().getName() = "SanitizeFilePath" or
        mc.getTarget().getName() = "RedactText"
      )
    )
  }

  override predicate checks(Expr e, AbstractValue v) {
    e = this.asExpr().(MethodCall).getAnArgument() and
    v instanceof Tainted
  }
}

from DataFlow::PathNode source, DataFlow::PathNode sink
where LogForging::flowPath(source, sink)
select sink.getNode(), source, sink, "This log entry depends on a user-provided value."
