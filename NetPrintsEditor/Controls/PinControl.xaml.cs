﻿using NetPrints.Graph;
using NetPrintsEditor.Commands;
using NetPrintsEditor.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NetPrintsEditor.Controls
{
    /// <summary>
    /// Interaction logic for PinControl.xaml
    /// </summary>
    public partial class PinControl : UserControl
    {
        public static readonly DependencyProperty ParentNodeControlProperty = DependencyProperty.Register(
            nameof(ParentNodeControl), typeof(NodeControl), typeof(PinControl));

        public NodeControl ParentNodeControl
        {
            get => (NodeControl)GetValue(ParentNodeControlProperty);
            set => SetValue(ParentNodeControlProperty, value);
        }

        private void OnLayoutUpdated(object sender, EventArgs e)
        {
            if (Pin != null)
            {
                Pin.PositionX = connector.Width / 2;
                Pin.PositionY = connector.Height / 2;

                if (ParentNodeControl != null && connector.FindCommonVisualAncestor(ParentNodeControl) != null)
                {
                    Pin.NodeRelativePosition = connector.TransformToVisual(ParentNodeControl).Transform(Pin.Position);
                }
            }
        }

        public static readonly DependencyProperty PinProperty =
            DependencyProperty.Register(nameof(Pin), typeof(NodePinVM), typeof(PinControl));
        
        public NodePinVM Pin
        {
            get => GetValue(PinProperty) as NodePinVM;
            set => SetValue(PinProperty, value);
        }

        public PinControl()
        {
            InitializeComponent();
            LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == PinProperty && Pin != null)
            {
                if (Pin.Pin is NodeInputDataPin || Pin.Pin is NodeInputExecPin)
                {
                    grid.ColumnDefinitions[0].Width = new GridLength(20, GridUnitType.Pixel);
                    grid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                    connector.SetValue(Grid.ColumnProperty, 0);
                    label.SetValue(Grid.ColumnProperty, 2);
                    label.HorizontalContentAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                    grid.ColumnDefinitions[2].Width = new GridLength(20, GridUnitType.Pixel);
                    connector.SetValue(Grid.ColumnProperty, 2);
                    label.SetValue(Grid.ColumnProperty, 0);
                    label.HorizontalContentAlignment = HorizontalAlignment.Right;
                }

                Color newColor = Color.FromArgb(0xFF, 0xFF, 0x00, 0x00);

                if (Pin.Pin is NodeInputDataPin)
                {
                    newColor = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xFF);
                }
                else if(Pin.Pin is NodeOutputDataPin)
                {
                    newColor = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xFF);
                }
                else if(Pin.Pin is NodeInputExecPin)
                {
                    newColor = Color.FromArgb(0xFF, 0xE0, 0xFF, 0xE0);
                }
                else if(Pin.Pin is NodeOutputExecPin)
                {
                    newColor = Color.FromArgb(0xFF, 0xE0, 0xFF, 0xE0);
                }

                SolidColorBrush brush = new SolidColorBrush(newColor);
                ellipse.Fill = brush;
                cable.Stroke = brush;
            }
        }

        private void OnEllipseMouseMove(object sender, MouseEventArgs e)
        {
            if(sender is Ellipse el && e.LeftButton == MouseButtonState.Pressed)
            {
                DragDrop.DoDragDrop(el, Pin, DragDropEffects.Link);
            }
        }

        private void OnEllipseDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(NodePinVM)))
            {
                // Another pin was dropped on this pin, link it

                NodePinVM droppedPin = e.Data.GetData(typeof(NodePinVM)) as NodePinVM;

                UndoRedoStack.Instance.DoCommand(NetPrintsCommands.ConnectPins, new NetPrintsCommands.ConnectPinsParameters()
                {
                    PinA = droppedPin,
                    PinB = Pin
                });

                e.Handled = true;
            }
        }

        private void OnEllipseDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (e.Data.GetDataPresent(typeof(NodePinVM)))
            {
                // Another pin is being hovered over this one, see if it can be linked to this pin

                NodePinVM draggingPin = e.Data.GetData(typeof(NodePinVM)) as NodePinVM;
                
                if(GraphUtil.CanConnectNodePins(draggingPin.Pin, Pin.Pin))
                {
                    e.Effects = DragDropEffects.Link;
                }

                e.Handled = true;
            }
        }

        // Needed so dragging doesnt happen
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
