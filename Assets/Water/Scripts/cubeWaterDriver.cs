using UnityEngine;
using System.Collections;

[System.Serializable]
public class waterMethod {
	public Shader waterShader;
	public Shader androidOptional;	//Because flat out Android is lagging behind the rest of the world
	public bool bElevated=true;	//Does this one render in 3D and should we be re-setting the height used by wakes and trails?
}

[ExecuteInEditMode]
public class cubeWaterDriver : MonoBehaviour {
	private static cubeWaterDriver instance;
	public static cubeWaterDriver Instance { get { return instance; } }
	public waterMethod[] waterLevels;

	public Camera mainCam;
	public Material cubeWater;
	public Vector2 yCastBounds = Vector2.one;
	public Vector4 bumpPan = Vector4.zero;
	public Vector4 wavePan = Vector4.zero;
	public Vector4 foamPan = Vector4.zero;
	Vector4 waterPosition = Vector4.zero, wavePosition = Vector4.zero, foamPosition = Vector4.zero;
	public bool bUpdateMesh = true;

	//public float extendLerp = 1.1f;

	public MeshFilter projectionFilter;
	public Mesh projectionMesh;
	public Bounds meshBounds;
	Vector4 bumpTiling, waveTiling, foamTiling, peakDirection;

	public GameObject viewTargetObject;

	float waveHeight = 1.125f;  //Setting this so there's a fallback just in case of a failure somewhere else

	public float customMarkOffset = -1.2f;
	int _currentLevel = 0;

	Vector4 RippleTiling;
	float _WaveHeight;
	public Texture2D Offsetmap;
	public void setShaderLevel(int level) {
		_currentLevel = level;
#if UNITY_ANDROID
		if (waterLevels[level].androidOptional!=null) {
			cubeWater.shader = waterLevels[level].androidOptional;
		} else {
			cubeWater.shader = waterLevels[level].waterShader;
		}
#else
		cubeWater.shader = waterLevels[level].waterShader;
#endif
		PlayerPrefs.SetInt("water", level);
		//Send through if we should flatten things or not.
		setGlobals(level);
	}

	void setGlobals(int level) {
		//Shader.SetGlobalFloat ("_WaveHeight", waterLevels[level].bElevated ? waveHeight*1.1f : 0f);	//And a small twiddle factor to keep things drawing above other things and cater for the reduced polygon spread in the wakes
		Shader.SetGlobalFloat("_WaveHeight", waterLevels[level].bElevated ? waveHeight : 0f);   //And a small twiddle factor to keep things drawing above other things and cater for the reduced polygon spread in the wakes
		Shader.SetGlobalFloat("_DownSet", waterLevels[level].bElevated ? customMarkOffset : 0f);
	}

	//We need buttons as Muvea decided that we had to use buttons
	public void setPlain(bool state) {
		if (state) {
			setShaderLevel(3);
		}
	}

	public void setLow(bool state) {
		if (state) {
			setShaderLevel(2);
		}
	}

	public void setMedium(bool state) {
		if (state) {
			setShaderLevel(1);
		}
	}

	public void setHigh(bool state) {
		if (state) {
			setShaderLevel(0);
		}
	}

	void Start() {
		//Will have to grab the correct shader at this stage
		if (cubeWaterDriver.Instance != null) {
			Debug.LogError("More than one copy of cubeWaterDriver.Instance");
			Destroy(this);
		}

		instance = this;

		if (!Application.isEditor) {    //Make no chance of this getting out least all hell breaks loose
			bUpdateMesh = false;
		}

		if (projectionFilter) {
			projectionMesh = projectionFilter.sharedMesh;
			projectionMesh.bounds = meshBounds;
		}

		/*
		bumpTiling = cubeWater.GetVector("_BumpTiling");
		waveTiling = cubeWater.GetVector("_RippleTiling");
		foamTiling = cubeWater.GetVector("_FoamTiling");
		peakDirection = cubeWater.GetVector("_PeakDirection");
		Shader.SetGlobalVector("_PeakDirection", peakDirection);
		//put our universal information across from the water
		Vector4 rippleTiling = cubeWater.GetVector("_RippleTiling");
		Shader.SetGlobalVector("_RippleTiling", rippleTiling);
		Shader.SetGlobalFloat("_DownSet", -0.7f);
		waveHeight = cubeWater.GetFloat("_WaveHeight");
		Shader.SetGlobalFloat("_WaveHeight", waveHeight * 1.1f);    //And a small twiddle factor to keep things drawing above other things and cater for the reduced polygon spread in the wakes
		RippleTiling = cubeWater.GetVector("_RippleTiling");    //So that this'll always be updated
		_WaveHeight = cubeWater.GetFloat("_WaveHeight
		*/
	}

	Vector3 bilinearDirectionPosition(Vector2 position) {
		Vector3 lerpA = Vector3.Lerp(cornerUR, cornerUL, position.x);
		Vector3 lerpB = Vector3.Lerp(cornerBL, cornerBR, position.x);
		Vector3 lerpC = Vector3.Lerp(lerpB, lerpA, position.y);
		return mainCam.transform.position - lerpC * mainCam.transform.position.y / lerpC.y;
	}

	Vector3 cornerUL = Vector3.zero, cornerUR = Vector3.zero, cornerBR = Vector3.zero, cornerBL = Vector3.zero, centerCam = Vector3.zero;
	//Ray viewRay;
	Vector3 viewRay;
	Vector3 waterViewRay(Vector2 point) {
		//This also needs to take into account any offset that we might get using our wave vector
		//_PeakDirection => peakDirection...
		viewRay = mainCam.ViewportPointToRay(point).direction.normalized;   //Gives us a ray through the viewport based off of this coordinate
		viewRay.y = Mathf.Clamp(viewRay.y, yCastBounds.x, yCastBounds.y);
		//get the world position of this ray
		Vector3 worldPoint = mainCam.transform.position + viewRay * mainCam.transform.position.y / viewRay.y;
		worldPoint += new Vector3(peakDirection.x, 0, peakDirection.z) * waveHeight;
		//The last thing to handle is the peakDirection.y*waveHeight for the offset detail...
		worldPoint += new Vector3(viewRay.x, 0f, viewRay.z).normalized * peakDirection.y * waveHeight;
		return (mainCam.transform.position - worldPoint).normalized;
		//we also need to limit our water plane distance so as not to get degredation on lesser graphics cards
		//return viewRay;
	}

	float rayMagnitude(Vector3 ray) {
		return (ray * mainCam.transform.position.y / ray.y).sqrMagnitude;
	}

	public Vector2 vCornerUL = new Vector2(-0.05f, 1f), vCornerUR = new Vector2(1.05f, 1f), vCornerBR = new Vector2(1.05f, -0.05f), vCornerBL = new Vector2(-0.05f, -0.05f), vCentreU = new Vector2(0.5f, 1f), vCentreB = new Vector2(0.5f, -0.05f);

	public float warpFactor = 0.2f, falloff = 2f;
	Vector2 warpUV(Vector2 uv, Vector2 viewCenter, float factor) {
		//Need to convert viewCenter into assumed UV coordinates
		//So we need a factor in UV space for this warp to happen across...
		float skeinFactor = 1f - Mathf.Clamp01(((uv - viewCenter).sqrMagnitude * falloff)); //0 to about 2...although the edges still need to remain intact, so "warpability" should be defined by X and Y being close to 0.5
		return Vector2.Lerp(uv, viewCenter, factor * skeinFactor);
	}

	public Texture2D defaultTex;

	public void setTexture(Texture2D thisTex) {
		if (thisTex != null) {
			cubeWater.SetTexture("_MainTex", thisTex);
		} else {
			cubeWater.SetTexture("_MainTex", defaultTex);
		}
	}

	public void setTexPosition(Vector4 newPos) {
		cubeWater.SetVector("_floorPos", newPos);
	}

	void updateMesh() {
		cornerUL = waterViewRay(vCornerUL);
		cornerUR = waterViewRay(vCornerUR);
		cornerBL = waterViewRay(vCornerBR);
		cornerBR = waterViewRay(vCornerBL);

		Vector2 viewUV = new Vector2(0.5f, 0.5f);
		if (viewTargetObject != null)
		{
			viewCenterPosition.y = 0;   //Drop this value down to floor		
			viewTargetObject.transform.position = viewCenterPosition;
			Vector3 viewCenter = mainCam.WorldToViewportPoint(viewCenterPosition); //mainCam.WorldToViewportPoint(viewCenterPosition);
			viewUV = new Vector2(1 - viewCenter.x, 0.9f - viewCenter.y);    //0.9 as a small drop to cater for the extended bounds of the mesh so that the clustered detail is always at the bottom of the boat
		}
		Vector3[] verts = new Vector3[projectionMesh.vertexCount];
		if (projectionMesh) {
			for (int i = 0; i < projectionMesh.vertexCount; i++) {
				//So if we're doing a detail cluster we need to be able to lerp the UVs in a falloff manner towards a point by a factor...
				//verts[i] = bilinearDirectionPosition(projectionMesh.uv[i]);
				verts[i] = bilinearDirectionPosition(warpUV(projectionMesh.uv[i], viewUV, warpFactor));
			}
		}

		projectionMesh.vertices = verts;
	}

	Vector3 viewFloorPos;
	Vector4 camViewPosition;
	Vector4 shiftedWavePosition;
	Vector3 viewCenterPosition; //where is our point of focus?

	public Vector4 texViewOffset = new Vector4(0, 0, 0, 0);

	int _yachtCount = -1, _raceID = -1;

	public bool getCameraFocus(out Vector3 viewCenterPosition)
	{
		//From memory this is where the camera's point of interest is supposed to be. It'll be out player, but for now it can be zero
		viewCenterPosition = new Vector3(0, 0, 20);
		return true;
	}


	void LateUpdate () {
		/*
		if (StateController.Instance!=null) {
			if (StateController.Instance.getActiveYachtRace()!=null) {
				if (StateController.Instance.activeRaceID !=_raceID) {
					_raceID = StateController.Instance.activeRaceID;
					_yachtCount = -1;
				}

				if (_yachtCount!=StateController.Instance.getActiveYachtRace().yachts.Count) {
					_yachtCount = StateController.Instance.getActiveYachtRace().yachts.Count;
					setGlobals(_currentLevel);	//Make sure we're up to speck with all of the details for our yachts
				}
			}
		}*/

		if (bUpdateMesh) {
			updateMesh();
		}

		//We dont' need these as globals...yet
		cubeWater.SetVector("cornerUL", waterViewRay(vCornerUL));
		cubeWater.SetVector("cornerUR", waterViewRay(vCornerUR));
		cubeWater.SetVector("cornerBR", waterViewRay(vCornerBR));
		cubeWater.SetVector("cornerBL", waterViewRay(vCornerBL));
		cubeWater.SetVector("centreB", waterViewRay(vCentreB));
		cubeWater.SetVector("centreU", waterViewRay(vCentreU));
		cubeWater.SetVector("camPos", mainCam.transform.position);
		//Pan our bump maps
		waterPosition += bumpPan * Time.deltaTime; ///Time.timeScale;
		wavePosition += wavePan * Time.deltaTime;///Time.timeScale;
		foamPosition += foamPan * Time.deltaTime;///Time.timeScale;
		//and vector control this:
		waterPosition.x = Mathf.Repeat (waterPosition.x, 1f);
		waterPosition.y = Mathf.Repeat (waterPosition.y, 1f);
		waterPosition.z = Mathf.Repeat (waterPosition.z, 1f);
		waterPosition.w = Mathf.Repeat (waterPosition.w, 1f);
		//Position of larger waves
		wavePosition.x = Mathf.Repeat (wavePosition.x, 1f);
		wavePosition.y = Mathf.Repeat (wavePosition.y, 1f);
		wavePosition.z = Mathf.Repeat (wavePosition.z, 1f);
		wavePosition.w = Mathf.Repeat (wavePosition.w, 1f);
		//position our foam
		foamPosition.x = Mathf.Repeat (foamPosition.x, 1f);
		foamPosition.y = Mathf.Repeat (foamPosition.y, 1f);
		foamPosition.z = Mathf.Repeat (foamPosition.z, 1f);
		foamPosition.w = Mathf.Repeat (foamPosition.w, 1f);
		//globals are used in more than one shader
		//Shader.SetGlobalVector("_waveTransOffset", wavePosition); //This never really gets high enough to matter

		if (mainCam!=null) {
			if (getCameraFocus(out viewCenterPosition)) {
				viewCenterPosition.y = 0;	//Drop this value down to floor
				Vector3 viewCenter = mainCam.WorldToViewportPoint(viewCenterPosition); //mainCam.WorldToViewportPoint(viewCenterPosition);
				Vector2 viewUV = new Vector2(viewCenter.x, 0.9f-viewCenter.y); //0.9 as a small drop to cater for the extended bounds of the mesh so that the clustered detail is always at the bottom of the boat
				Shader.SetGlobalVector("viewCenter", viewUV);
				//shift-position our bumpmap
				camViewPosition = new Vector4(viewCenterPosition.x*bumpTiling.x, viewCenterPosition.z*bumpTiling.y, viewCenterPosition.x*bumpTiling.z, viewCenterPosition.z*bumpTiling.w);
				camViewPosition = new Vector4(camViewPosition.x - camViewPosition.x%1, camViewPosition.y - camViewPosition.y%1, camViewPosition.z - camViewPosition.z%1, camViewPosition.w - camViewPosition.w%1);
				shiftedWavePosition = waterPosition - camViewPosition;
				Shader.SetGlobalVector("_bumpTransOffset", shiftedWavePosition);
				//Shift position for foam effects
				camViewPosition = new Vector4(viewCenterPosition.x*foamTiling.x, viewCenterPosition.z*foamTiling.y, viewCenterPosition.x*foamTiling.z, viewCenterPosition.z*foamTiling.w);
				camViewPosition = new Vector4(camViewPosition.x - camViewPosition.x%1, camViewPosition.y - camViewPosition.y%1, camViewPosition.z - camViewPosition.z%1, camViewPosition.w - camViewPosition.w%1);
				shiftedWavePosition = foamPosition - camViewPosition;
				Shader.SetGlobalVector("_foamTransOffset", shiftedWavePosition);

				meshBounds.center = viewCenterPosition;
				projectionMesh.bounds = meshBounds;

				//Wave position as even that has been causing issues
				camViewPosition = new Vector4(viewCenterPosition.x*waveTiling.x, viewCenterPosition.z*waveTiling.y, viewCenterPosition.x*waveTiling.z, viewCenterPosition.z*waveTiling.w);
				camViewPosition = new Vector4(camViewPosition.x - camViewPosition.x%1, camViewPosition.y - camViewPosition.y%1, camViewPosition.z - camViewPosition.z%1, camViewPosition.w - camViewPosition.w%1);
				shiftedWavePosition = wavePosition - camViewPosition;
				Shader.SetGlobalVector("_waveTransOffset", shiftedWavePosition);
			}
		}
	}

	

	public float getWaveHeightAtPoint(Vector3 position)
    {
		Vector4 rippleCoords = new Vector4(transform.position.x * RippleTiling.x, transform.position.z * RippleTiling.y, transform.position.x * RippleTiling.z, transform.position.z * RippleTiling.w) + shiftedWavePosition;
		Color height = Offsetmap.GetPixelBilinear(rippleCoords.x, rippleCoords.y);
		height += Offsetmap.GetPixelBilinear(rippleCoords.z, rippleCoords.w);
		float waveHeight = height.r;
		waveHeight -= 0.25f;
		waveHeight *= _WaveHeight;
		/*
		 * o.rippleCoords = o.floorUV.xzxz * _RippleTiling.xyzw + _waveTransOffset.xyzw;
		float height = tex2Dlod(_Offsetmap, float4(o.rippleCoords.xy, 0, lod)).r;
		height = (height + tex2Dlod(_Offsetmap, float4(o.rippleCoords.wz, 0, lod)).r) * 0.5;
		height -= 0.25; //Bring this down to halfway so the water level is at the flat
						//o.peakColor = saturate(height*4-2);	//used for figuring out where to put foam peaks down. Hard coded might not be the best way to do this
		v.vertex.y += _WaveHeight * 0.5 * height;// *saturate(1.0 - dist);
		*/
		//So for this we need to sample our texture and calculate what the offset should be to get our water level

		return waveHeight;
    }
}
