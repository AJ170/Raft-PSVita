using UnityEngine;
using System.Collections;
using System.IO;

[ExecuteInEditMode]
public class sliceImage : MonoBehaviour {
	public Texture2D targetImage;
	float width=3, height=2;
	public bool bSlice=false;

	// Update is called once per frame
	void Update () {
		if (bSlice) {
			bSlice=false;
			processImage();
		}
	}

	void processImage() {
		//targetImage.ReadPixels(new Rect(0,0,targetImage.width/width, targetImage.height/height), 0,0,false);
		//newTexture.Apply();
		Color[] colorArrayCropped = targetImage.GetPixels(0,0, (int)(targetImage.width/width), (int)(targetImage.height/height));
		Texture2D croppedTex = new Texture2D((int)(targetImage.width/width),(int)(targetImage.height/height), TextureFormat.ARGB32, false);
		croppedTex.SetPixels(colorArrayCropped);
		//croppedTex = FillInClear(croppedTex, Color.white);	//Don't see a need to do this.
		croppedTex.Apply();
		byte[] bytes = croppedTex.EncodeToPNG();
#if !UNITY_WEBPLAYER
		File.WriteAllBytes(Path.Combine(Application.streamingAssetsPath, "cutImg.png"), bytes);
#endif
	}

	public Texture2D FillInClear(Texture2D tex2D, Color whatToFillWith) {
		
		for(int i = 0; i < tex2D.width; i++) {
			for(int j = 0; j < tex2D.height; j++) {
				if(tex2D.GetPixel(i,j) == Color.clear)
					tex2D.SetPixel(i,j, whatToFillWith);
			}
		}
		return tex2D;
	}
}