namespace MoonSharp.Interpreter
{
    public enum AnnotationAction
    {
        Allow,
        Ignore,
        Error
    }
    
    public interface IAnnotationPolicy
    {
        AnnotationAction OnChunkAnnotation(string name, DynValue value);
        AnnotationAction OnFunctionAnnotation(string name, DynValue value);
    }

    public static class AnnotationPolicies
    {
        public static IAnnotationPolicy Allow { get; } = new AllowPolicy();

        public static IAnnotationPolicy Ignore { get; } = new IgnorePolicy();

        public static IAnnotationPolicy Error { get; } = new ErrorPolicy();
        
        class AllowPolicy : IAnnotationPolicy
        {
            public AnnotationAction OnChunkAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Allow;
            }

            public AnnotationAction OnFunctionAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Allow;
            }
        }
        
        class IgnorePolicy : IAnnotationPolicy
        {
            public AnnotationAction OnChunkAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Ignore;
            }

            public AnnotationAction OnFunctionAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Ignore;
            }
        }
        
        class ErrorPolicy : IAnnotationPolicy
        {
            public AnnotationAction OnChunkAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Error;
            }

            public AnnotationAction OnFunctionAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Error;
            }
        }
    }
}