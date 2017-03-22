﻿using NetPrints.Core;
using NetPrints.Graph;
using NetPrintsEditor.Commands;
using NetPrintsEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NetPrintsEditor.Controls
{
    /// <summary>
    /// Interaction logic for FunctionEditorControl.xaml
    /// </summary>
    public partial class MethodEditorControl : UserControl
    {
        public const double GridCellSize = 20;

        private double drawCanvasScale = 1;
        private const double DrawCanvasMinScale = 0.3;
        private const double DrawCanvasMaxScale = 1.0;
        private const double DrawCanvasScaleFactor = 1.3;

        public MethodVM Method
        {
            get => GetValue(MethodProperty) as MethodVM;
            set => SetValue(MethodProperty, value);
        }

        public static DependencyProperty MethodProperty = DependencyProperty.Register(
            nameof(Method), typeof(MethodVM), typeof(MethodEditorControl));
        
        public static DependencyProperty SuggestionsProperty = DependencyProperty.Register(
            nameof(Suggestions), typeof(ObservableRangeCollection<object>), typeof(MethodEditorControl));

        public ObservableRangeCollection<object> Suggestions
        {
            get => (ObservableRangeCollection<object>)GetValue(SuggestionsProperty);
            set => SetValue(SuggestionsProperty, value);
        }

        public MethodEditorControl()
        {
            InitializeComponent();
        }

        public void ShowVariableGetSet(VariableGetSetInfo variableInfo, Point? position = null)
        {
            // Check that the tag is unused
            if(variableInfo.Tag != null)
            {
                throw new ArgumentException("variableInfo needs to have its Tag set to null because it is used for position");
            }

            // Use current mouse position if position is not set
            Point pos = position ?? Mouse.GetPosition(drawCanvas);

            variableInfo.Tag = pos;
            variableGetSet.VariableInfo = variableInfo;

            Canvas.SetLeft(variableGetSet, pos.X - variableGetSet.Width / 2);
            Canvas.SetTop(variableGetSet, pos.Y - variableGetSet.Height / 2);

            variableGetSet.Visibility = Visibility.Visible;
        }

        public void HideVariableGetSet()
        {
            variableGetSet.VariableInfo = null;
            variableGetSet.Visibility = Visibility.Hidden;
        }

        private void OnVariableGetSetMouseLeave(object sender, MouseEventArgs e)
        {
            HideVariableGetSet();
        }

        private void OnVariableSetClicked(VariableGetSetControl sender,
            VariableGetSetInfo variableInfo, bool wasSet)
        {
            Point position;

            // Try to get the spawn position from the variableInfo's Tag
            // Otherwise use current mouse location

            if(variableInfo.Tag is Point infoPosition)
            {
                position = infoPosition;
            }
            else
            {
                position = Mouse.GetPosition(drawCanvas);
            }

            if (wasSet)
            {
                // VariableSetterNode(Method method, TypeSpecifier targetType, 
                // string variableName, TypeSpecifier variableType) 

                UndoRedoStack.Instance.DoCommand(NetPrintsCommands.AddNode, new NetPrintsCommands.AddNodeParameters
                (
                    typeof(VariableSetterNode), Method.Method, position.X, position.Y,
                    variableInfo.TargetType, variableInfo.Name, variableInfo.Type
                ));
            }
            else
            {
                // VariableGetterNode(Method method, TypeSpecifier targetType, 
                // string variableName, TypeSpecifier variableType) 

                UndoRedoStack.Instance.DoCommand(NetPrintsCommands.AddNode, new NetPrintsCommands.AddNodeParameters
                (
                    typeof(VariableGetterNode), Method.Method, position.X, position.Y,
                    variableInfo.TargetType, variableInfo.Name, variableInfo.Type
                ));
            }

            HideVariableGetSet();
        }

        private void OnGridDrop(object sender, DragEventArgs e)
        {
            if (Method != null && e.Data.GetDataPresent(typeof(VariableVM)))
            {
                VariableVM variable = e.Data.GetData(typeof(VariableVM)) as VariableVM;

                bool canSet = !(variable.Modifiers.HasFlag(VariableModifiers.ReadOnly) ||
                    variable.Modifiers.HasFlag(VariableModifiers.Const));

                VariableGetSetInfo variableInfo = new VariableGetSetInfo(
                    variable.Name, variable.VariableType, true, canSet, Method.Class.Type);

                ShowVariableGetSet(variableInfo, e.GetPosition(drawCanvas));

                e.Handled = true;
            }
            else if(e.Data.GetDataPresent(typeof(NodePinVM)))
            {
                // Show all relevant methods for the type of the pin if its a data pin

                NodePinVM pin = e.Data.GetData(typeof(NodePinVM)) as NodePinVM;
                
                if (pin.Pin is NodeOutputDataPin odp)
                {
                    // Add public methods
                    Suggestions = new ObservableRangeCollection<object>(
                        ReflectionUtil.GetPublicMethodsForType(odp.PinType));

                    // Add properties
                    Suggestions.AddRange(ReflectionUtil.GetPublicPropertiesForType(
                        odp.PinType, Method?.Class?.Project?.LoadedAssemblies));
                }
                else if (pin.Pin is NodeInputDataPin idp)
                {
                    Suggestions = new ObservableRangeCollection<object>(ReflectionUtil.GetStaticFunctionsWithReturnType(
                        idp.PinType, Method?.Class?.Project?.LoadedAssemblies));
                }
                else if (pin.Pin is NodeOutputExecPin oxp)
                {
                    pin.ConnectedPin = null;

                    Suggestions = new ObservableRangeCollection<object>(ReflectionUtil.GetPublicMethodsForType(
                        Method.Class.SuperType))
                    {
                        typeof(ForLoopNode),
                        typeof(IfElseNode),
                        typeof(ConstructorNode),
                    };
                }
                else if(pin.Pin is NodeInputExecPin ixp)
                {
                    Suggestions = new ObservableRangeCollection<object>(
                        ReflectionUtil.GetStaticFunctions(Method?.Class?.Project?.LoadedAssemblies))
                    {
                        typeof(ForLoopNode),
                        typeof(IfElseNode),
                        typeof(ConstructorNode),
                    };
                }
                else
                {
                    // Unknown type, no suggestions
                    Suggestions = new ObservableRangeCollection<object>();
                }
                
                // Open the context menu
                grid.ContextMenu.PlacementTarget = grid;
                grid.ContextMenu.IsOpen = true;

                e.Handled = true;
            }
            if (Method != null && e.Data.GetDataPresent(typeof(MethodVM)))
            {
                Point mousePosition = e.GetPosition(methodEditorWindow);
                MethodVM method = e.Data.GetData(typeof(MethodVM)) as MethodVM;

                if (method.Modifiers.HasFlag(MethodModifiers.Static))
                {
                    // CallStaticFunctionNode(Method method, TypeSpecifier classType, 
                    // string methodName, IEnumerable<TypeSpecifier> inputTypes, 
                    // IEnumerable<TypeSpecifier> outputTypes)

                    UndoRedoStack.Instance.DoCommand(NetPrintsCommands.AddNode, new NetPrintsCommands.AddNodeParameters
                    (
                        typeof(CallStaticFunctionNode), Method.Method, mousePosition.X, mousePosition.Y,
                        method.Class.Type, method.Name, method.ArgumentTypes, method.ReturnTypes
                    ));
                }
                else
                {
                    // CallMethodNode(Method method, TypeSpecifier targetType, string methodName, 
                    // IEnumerable<TypeSpecifier> inputTypes, IEnumerable<TypeSpecifier> outputTypes)

                    UndoRedoStack.Instance.DoCommand(NetPrintsCommands.AddNode, new NetPrintsCommands.AddNodeParameters
                    (
                        typeof(CallMethodNode), Method.Method, mousePosition.X, mousePosition.Y,
                        method.Class.Type, method.Name, method.ArgumentTypes, method.ReturnTypes
                    ));
                }

                e.Handled = true;
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (Method != null && e.Data.GetDataPresent(typeof(VariableVM)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else if(e.Data.GetDataPresent(typeof(NodePinVM)))
            {
                // Set connecting position to the correct relative mouse position
                NodePinVM pin = e.Data.GetData(typeof(NodePinVM)) as NodePinVM;
                pin.ConnectingAbsolutePosition = e.GetPosition(drawCanvas);

                e.Effects = DragDropEffects.Link;
                e.Handled = true;
            }
            else if(Method != null && e.Data.GetDataPresent(typeof(MethodVM)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (Method != null)
            {
                Suggestions = new ObservableRangeCollection<object>(ReflectionUtil.GetStaticFunctions(
                    Method.Class?.Project?.LoadedAssemblies))
                {
                    typeof(ForLoopNode),
                    typeof(IfElseNode),
                    typeof(ConstructorNode),
                };
            }
            else
            {
                Suggestions?.Clear();
            }
        }

        private void OnMouseWheelScroll(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0)
            {
                drawCanvasScale /= DrawCanvasScaleFactor;
            }
            else
            {
                drawCanvasScale *= DrawCanvasScaleFactor;
            }

            // Clamp scale between min and max
            if(drawCanvasScale < DrawCanvasMinScale)
            {
                drawCanvasScale = DrawCanvasMinScale;
            }
            else if(drawCanvasScale > DrawCanvasMaxScale)
            {
                drawCanvasScale = DrawCanvasMaxScale;
            }

            drawCanvas.LayoutTransform = new ScaleTransform(drawCanvasScale, drawCanvasScale);
            e.Handled = true;
        }

        private void OnDrawCanvasLeftMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Deselect node

            if (Method != null)
            {
                Method.SelectedNode = null;
            }
        }

        #region Commands
        private void OpenVariableGetSetCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter is VariableGetSetInfo variableInfo;
        }

        private void OpenVariableGetSetExecute(object sender, ExecutedRoutedEventArgs e)
        {
            grid.ContextMenu.IsOpen = false;
            ShowVariableGetSet((VariableGetSetInfo)e.Parameter);
        }
        #endregion
    }
}
