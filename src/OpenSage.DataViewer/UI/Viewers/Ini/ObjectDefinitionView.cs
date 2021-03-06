﻿using System;
using System.Linq;
using System.Numerics;
using Eto.Forms;
using OpenSage.Data.Ini;
using OpenSage.DataViewer.Controls;
using OpenSage.Graphics.Cameras.Controllers;
using OpenSage.Logic.Object;

namespace OpenSage.DataViewer.UI.Viewers.Ini
{
    public sealed class ObjectDefinitionView : Splitter
    {
        private readonly ListBox _listBox;
        private readonly ObjectComponent _objectComponent;

        public ObjectDefinitionView(Func<IntPtr, Game> createGame, ObjectDefinition objectDefinition)
        {
            var scene = new Scene();

            var objectEntity = Entity.FromObjectDefinition(objectDefinition);
            _objectComponent = objectEntity.GetComponent<ObjectComponent>();
            scene.Entities.Add(objectEntity);

            scene.CameraController = new ArcballCameraController(
                Vector3.Zero,
                200);

            _listBox = new ListBox();
            _listBox.Width = 200;
            _listBox.ItemTextBinding = Binding.Property((BitArray<ModelConditionFlag> x) => x.DisplayName);
            _listBox.SelectedValueChanged += (sender, e) =>
            {
                var modelConditionState = (BitArray<ModelConditionFlag>) _listBox.SelectedValue;
                _objectComponent.SetModelConditionFlags(modelConditionState);
            };
            _listBox.DataStore = _objectComponent.ModelConditionStates.ToList();
            _listBox.SelectedIndex = 0;

            Panel1 = _listBox;

            Panel2 = new GameControl
            {
                CreateGame = h =>
                {
                    var game = createGame(h);

                    game.Scene = scene;

                    return game;
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            Panel1.Dispose();
            Panel2.Dispose();

            base.Dispose(disposing);
        }
    }
}
