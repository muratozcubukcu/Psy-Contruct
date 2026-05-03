using System;
using UnityEngine;

public class MinimapTrigger : MonoBehaviour {
    
    public class MinimapEventArgs : System.EventArgs {
        public bool entering;
        public MinimapEventArgs(bool entering) => this.entering = entering;
    }
    public event EventHandler<MinimapEventArgs> OnEnterMinimapRange;
    
    private void OnTriggerEnter2D(Collider2D other) {
        if (((1 << other.gameObject.layer) & LayerMask.GetMask("SpaceCraft")) == 0) return;
        
        OnEnterMinimapRange?.Invoke(this, new MinimapEventArgs(true));
    }
    
    private void OnTriggerExit2D(Collider2D other) {
        if (((1 << other.gameObject.layer) & LayerMask.GetMask("SpaceCraft")) == 0) return;
        
        OnEnterMinimapRange?.Invoke(this, new MinimapEventArgs(false));
    }
}
