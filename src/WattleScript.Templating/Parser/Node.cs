namespace WattleScript.Templating;

internal class Document
{
    public List<NodeBase> Nodes { get; set; }
    public INodeWithChildren CurrentNode { get; set; }
    DocumentNode DocNode = new DocumentNode();
    
    public Document()
    {
        Nodes = new List<NodeBase>();
        Nodes.Add(DocNode);
        CurrentNode = DocNode;
    }

    public void AddChild(NodeBase node)
    {
        DocNode.AddChild(node);
    }
}

internal interface INodeWithChildren
{
    public List<NodeBase> Children { get; set; }
    public void AddChild(NodeBase child);
}

internal class NodeBase
{
    public int CharFrom { get; set; }
    public bool Recovery { get; set; }
    public int Line { get; set; }
    public int Col { get; set; }
    public int ContentFrom { get; set; }
    public int ContentTo { get; set; }
}

internal class ServerNode : NodeBase
{
    
}

internal class TextNode : NodeBase
{
    public string Text { get; set; }

    public TextNode(string text)
    {
        Text = text;
    }
}

internal class DocumentNode : NodeBase, INodeWithChildren
{
    public List<NodeBase> Children { get; set; } = new List<NodeBase>();
    public void AddChild(NodeBase child)
    {
        Children.Add(child);
    }
}

internal class HtmlElement : NodeBase, INodeWithChildren
{
    internal enum ClosingType
    {
        Unknown,
        SelfClosing,
        ImplicitSelfClosing,
        EndTag
    }

    public HtmlElement Parent { get; set; }
    public List<HtmlAttribute> Attributes { get; set; } = new List<HtmlAttribute>();
    public string Name { get; set; }
    public ClosingType Closing { get; set; }
    public bool ForceNativeTag { get; set; }
    public List<NodeBase> Children { get; set; } = new List<NodeBase>();
    
    public void AddChild(NodeBase child)
    {
        Children.Add(child);
    }
}

internal class HtmlAttribute
{
    public string Name { get; set; }
    public string Value { get; set; }
    public Parser.HtmlAttrEnclosingModes QuoteType { get; set; }

    internal HtmlAttribute(string name, string value, Parser.HtmlAttrEnclosingModes quoteType)
    {
        Name = name;
        Value = value;
        QuoteType = quoteType;
    }
}