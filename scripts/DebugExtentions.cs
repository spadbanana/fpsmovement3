using Godot;
using System;

public static class DebugExtentions
{
    public static void UpdateDebugLabel (this Node originNode, string stringname)
    {        
        Label label = new();
        label.Text = stringname;

        originNode.GetTree().Root.GetNode<VBoxContainer>("World/Debug/PanelContainer/VBoxContainer").AddChild(label);
    }

    public static void RemoveLabels(this Node originNode)
    {
        var children = originNode.GetTree().Root.GetNode<VBoxContainer>("World/Debug/PanelContainer/VBoxContainer").GetChildren();
        foreach (Node child in children)
        {
            originNode.GetTree().Root.GetNode<VBoxContainer>("World/Debug/PanelContainer/VBoxContainer").RemoveChild(child);
        }
    }
        
}
