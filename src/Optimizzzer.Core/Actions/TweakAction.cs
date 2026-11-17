using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Optimizzzer.Core
{
    public enum ActionState { NotApplied, Applied, NotApplicable }

    /// <summary>The state an action saw before it changed anything, so it can be put back exactly.</summary>
    [DataContract]
    public sealed class ActionSnapshot
    {
        [DataMember(Order = 1)] public string Kind { get; set; }
        [DataMember(Order = 2)] public bool Existed { get; set; }
        [DataMember(Order = 3, EmitDefaultValue = false)] public string ValueKind { get; set; }
        [DataMember(Order = 4, EmitDefaultValue = false)] public string Data { get; set; }
        /// <summary>For registry writes: the first key in the path that did not exist yet, so undo can remove what we created.</summary>
        [DataMember(Order = 5, EmitDefaultValue = false)] public string CreatedKey { get; set; }
        [DataMember(Order = 6, EmitDefaultValue = false)] public RegTreeNode Tree { get; set; }
    }

    [DataContract]
    public sealed class RegTreeNode
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public List<RegTreeValue> Values { get; set; } = new List<RegTreeValue>();
        [DataMember(Order = 3)] public List<RegTreeNode> Children { get; set; } = new List<RegTreeNode>();
    }

    [DataContract]
    public sealed class RegTreeValue
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public string Kind { get; set; }
        [DataMember(Order = 3)] public string Data { get; set; }
    }

    public abstract class TweakAction
    {
        public abstract ActionState Detect(SystemContext ctx);
        public abstract ActionSnapshot Snapshot(SystemContext ctx);
        public abstract void Apply(SystemContext ctx);
        public abstract void Revert(SystemContext ctx, ActionSnapshot snapshot);
        /// <summary>Exact technical description shown in the review step.</summary>
        public abstract string Describe(Lang lang);
        public virtual bool Reversible => true;
    }
}
