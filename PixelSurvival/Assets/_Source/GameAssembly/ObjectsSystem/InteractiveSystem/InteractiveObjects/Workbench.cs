using System;
using GameAssembly.ObjectsSystem.View.ObjectsView;
using GameAssembly.UiSystem;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects
{
    public class Workbench : NetworkBehaviour, IInteractiveObject
    {
        [SerializeField] private int tier;
        [SerializeField] private SpriteRenderer workbenchRenderer;

        [Inject] private WorkbenchView _view;

        private void Start()
        {
            ObjectInjector.Inject(this);
        }

        public void Interact()
        {
            _view.Initialize(this);
            UiManager.Instance.OpenRequest(_view);
        }

        public int GetTier() => tier;

        public Renderer GetRendererTarget() => workbenchRenderer;
    }
}