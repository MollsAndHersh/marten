using System;
using System.Collections.Generic;
using System.Linq;
using Remotion.Linq.Parsing.ExpressionVisitors.Transformation.PredefinedTransformations;

namespace Marten.Events.Daemon.Resiliency
{
    public class ExceptionPolicy : IHandlerDefinition, IThenExpression
    {
        private readonly DaemonSettings _parent;
        private readonly List<Func<Exception, bool>> _filters = new List<Func<Exception, bool>>();

        internal ExceptionPolicy(DaemonSettings parent, Func<Exception, bool> filter)
        {
            _parent = parent;
            _filters.Add(filter);
        }

        internal List<IContinuation> Continuations { get; } = new List<IContinuation>();

        /// <summary>
        /// Specifies an additional type of exception that this policy can handle with additional filters on this exception type.
        /// </summary>
        /// <param name="exceptionPredicate">The exception predicate to filter the type of exception this policy can handle.</param>
        /// <returns>The PolicyBuilder instance.</returns>
        public ExceptionPolicy AndInner(Func<Exception, bool> exceptionPredicate)
        {
            _filters.Add(ex => ex.InnerException != null && exceptionPredicate(ex.InnerException));
            return this;
        }


        /// <summary>
        /// Specifies an additional type of exception that this policy can handle with additional filters on this exception type.
        /// </summary>
        /// <typeparam name="TException">The type of the exception.</typeparam>
        /// <param name="exceptionPredicate">The exception predicate to filter the type of exception this policy can handle.</param>
        /// <returns>The PolicyBuilder instance.</returns>
        public ExceptionPolicy AndInner<TException>(Func<TException, bool> exceptionPredicate)
            where TException : Exception
        {
            _filters.Add(ex => (ex.InnerException is TException e) && exceptionPredicate(e));
            return this;
        }

        /// <summary>
        ///     Specifies an additional type of exception that this policy can handle if found as an InnerException of a regular
        ///     <see cref="Exception" />, or at any level of nesting within an <see cref="AggregateException" />.
        /// </summary>
        /// <typeparam name="TException">The type of the exception to handle.</typeparam>
        /// <returns>The PolicyBuilder instance, for fluent chaining.</returns>
        public ExceptionPolicy AndInner<TException>() where TException : Exception
        {
            _filters.Add(ex => ex.InnerException is TException);
            return this;
        }

        internal bool Matches(Exception ex)
        {
            return _filters.All(x => x(ex));
        }

        /// <summary>
        /// Pause the execution of the current projection shard
        /// for the defined amount of time before attempting to restart
        /// </summary>
        /// <param name="timeSpan"></param>
        public void Pause(TimeSpan timeSpan)
        {
            Continuations.Add(new PauseProjection(timeSpan));
        }

        /// <summary>
        /// Pause all running projection shards for the defined amount
        /// of time before attempting to restart
        /// </summary>
        /// <param name="timeSpan"></param>
        public void PauseAll(TimeSpan timeSpan)
        {
            Continuations.Add(new PauseAllProjections(timeSpan));
        }

        /// <summary>
        /// Stop the running projection shard
        /// </summary>
        public void Stop()
        {
            Continuations.Add(new StopProjection());
        }

        /// <summary>
        /// Stop all running projections shards
        /// </summary>
        public void StopAll()
        {
            Continuations.Add(new StopAllProjections());
        }

        public IThenExpression RetryLater(params TimeSpan[] timeSpans)
        {
            Continuations.AddRange(timeSpans.Select(x => new RetryLater(x)));
            return this;
        }

        ICoreHandlerDefinition IThenExpression.Then => this;

        /// <summary>
        /// Ignore the exception and do nothing
        /// </summary>
        public void DoNothing()
        {
            Continuations.Add(new DoNothing());
        }
    }

    public interface ICoreHandlerDefinition
    {
        /// <summary>
        /// Pause the execution of the current projection shard
        /// for the defined amount of time before attempting to restart
        /// </summary>
        /// <param name="timeSpan"></param>
        void Pause(TimeSpan timeSpan);

        /// <summary>
        /// Pause all running projection shards for the defined amount
        /// of time before attempting to restart
        /// </summary>
        /// <param name="timeSpan"></param>
        void PauseAll(TimeSpan timeSpan);

        /// <summary>
        /// Stop the running projection shard
        /// </summary>
        void Stop();

        /// <summary>
        /// Stop all running projections shards
        /// </summary>
        void StopAll();

        /// <summary>
        /// Ignore the exception and do nothing
        /// </summary>
        void DoNothing();
    }

    public interface IThenExpression
    {
        /// <summary>
        /// Define the next operation after retrying
        /// a set number of times
        /// </summary>
        ICoreHandlerDefinition Then { get; }

    }

    public interface IHandlerDefinition : ICoreHandlerDefinition
    {
        /// <summary>
        /// Set a limited number of retry attempts for matching exceptions.
        /// Can be used to specify an exponential backoff strategy
        /// </summary>
        /// <param name="timeSpans"></param>
        /// <returns></returns>
        IThenExpression RetryLater(params TimeSpan[] timeSpans);
    }
}
