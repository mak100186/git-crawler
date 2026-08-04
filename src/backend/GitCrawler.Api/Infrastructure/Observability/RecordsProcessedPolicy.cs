using System.Reflection;

using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;

using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace GitCrawler.Api.Infrastructure.Observability;

// F-014: the raw-IChainPolicy half of this feature's Wolverine middleware - see
// ObservabilityMiddleware's header comment for why two idioms are needed together. Registered once,
// globally, via opts.Policies.Add<RecordsProcessedPolicy>() in Program.cs's UseWolverine(...) block,
// alongside opts.Policies.AddMiddleware<ObservabilityMiddleware>() - together they apply to every
// handler chain Wolverine compiles, with no per-handler opt-in anywhere (AC1/AC2).
//
// IChainPolicy.Apply runs once per chain, at Wolverine's own startup compilation step (not once per
// invocation), with full reflection access to that chain's actual handler MethodInfo - including its
// real, concrete return type. That is exactly what ObservabilityMiddleware.RecordSuccess<TResult>'s
// attribute-based counterpart cannot get hold of at all (see that class's comment: the
// attribute-convention parameter resolver only recognises a fixed set of well-known types, `object`
// isn't one of them, and it silently defaults to `new object()` instead of throwing or leaving it
// null). Here, the concrete return type is known statically per chain, so a closed generic
// RecordSuccess<TResult> MethodInfo can be built directly via reflection and wired into the chain as
// an ordinary postprocessor frame - Wolverine's own codegen then emits a call passing the exact,
// already-typed return variable by reference, with no runtime type discovery needed on the hot path.
public class RecordsProcessedPolicy : IChainPolicy
{
    private static readonly MethodInfo RecordSuccessOpenGeneric =
        typeof(ObservabilityMiddleware).GetMethod(nameof(ObservabilityMiddleware.RecordSuccess))
        ?? throw new InvalidOperationException(
            $"{nameof(ObservabilityMiddleware)}.{nameof(ObservabilityMiddleware.RecordSuccess)} was not found by reflection - has it been renamed or made non-public?");

    public void Apply(IReadOnlyList<IChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.OfType<HandlerChain>())
        {
            // The last handler call's return variable is null for a void/bare-Task
            // Handle/HandleAsync (a command with nothing meaningful to report back) - covered by the
            // no-generic-parameter overload instead, per this feature's own "no meaningful
            // records-processed count still gets a duration+success record, not an error" edge case.
            // .LastOrDefault(), not .Single(): Wolverine supports multiple handler classes per
            // message type (MultipleHandlerBehavior), though ADR-015's convention - one handler per
            // vertical slice - means every chain in this codebase has exactly one.
            var returnVariable = chain.HandlerCalls().LastOrDefault()?.ReturnVariable;
            if (returnVariable is null)
            {
                chain.Postprocessors.Add(new MethodCall(typeof(ObservabilityMiddleware), nameof(ObservabilityMiddleware.RecordSuccessVoid)));
                continue;
            }

            var closedRecordSuccess = RecordSuccessOpenGeneric.MakeGenericMethod(returnVariable.VariableType);
            var recordSuccessCall = new MethodCall(typeof(ObservabilityMiddleware), closedRecordSuccess);

            // Binds this specific chain's already-known return Variable directly, rather than
            // relying on Wolverine's own by-type auto-matching for this one argument. Matching by
            // type alone would in fact also work here (the closed generic's `result` parameter and
            // the return variable are now the exact same concrete type) - this is the deliberate,
            // explicit choice rather than an incidental one. Envelope/ILogger<T> on the same method
            // are still left to Wolverine's normal auto-matching, which resolves both correctly on
            // its own (verified empirically - see ObservabilityMiddleware's header comment).
            recordSuccessCall.TrySetArgument(returnVariable);

            // Postprocessors run only after the primary handler action succeeds, never on the
            // exception path (that path is ObservabilityMiddleware.OnException's job) - verified
            // empirically against WolverineFx 6.24.2 that a throwing handler skips every postprocessor
            // frame entirely and control passes straight to the OnException catch block, so a failed
            // invocation never double-logs both an OnException failure line and a RecordSuccess line.
            chain.Postprocessors.Add(recordSuccessCall);
        }
    }
}