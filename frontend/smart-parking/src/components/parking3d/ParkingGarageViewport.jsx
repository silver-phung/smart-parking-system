import { forwardRef, useEffect, useImperativeHandle, useRef } from "react";
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { buildGarageScene, disposeGarageScene } from "./garageScene.js";

const VIEW_POINTS = {
  WALK: {
    camera: new THREE.Vector3(-48, 1.75, 0),
    target: new THREE.Vector3(-34, 1.45, 0),
  },
  OVERVIEW: {
    camera: new THREE.Vector3(2, 69, 68),
    target: new THREE.Vector3(0, 0, 0),
  },
  TOP: {
    camera: new THREE.Vector3(0, 105, 0.1),
    target: new THREE.Vector3(0, 0, 0),
  },
};

function easeInOut(value) {
  return value < 0.5
    ? 2 * value * value
    : 1 - Math.pow(-2 * value + 2, 2) / 2;
}

const ParkingGarageViewport = forwardRef(function ParkingGarageViewport(
  { model, actor, viewMode, onNearVehicle, onElevatorProximity, onReady },
  ref,
) {
  const hostRef = useRef(null);
  const runtimeRef = useRef(null);
  const lastVehicleRef = useRef(null);
  const lastElevatorRef = useRef(false);
  const viewModeRef = useRef(viewMode);
  viewModeRef.current = viewMode;

  useImperativeHandle(ref, () => ({
    setView(mode) {
      runtimeRef.current?.setView(mode);
    },
    walkToElevator() {
      const runtime = runtimeRef.current;
      if (!runtime?.sceneResult?.elevatorAnchors?.length) return;
      const anchor = runtime.sceneResult.elevatorAnchors[1] || runtime.sceneResult.elevatorAnchors[0];
      const world = new THREE.Vector3();
      anchor.getWorldPosition(world);
      runtime.walkTo({ x: world.x, z: world.z - 1.6 });
    },
  }), []);

  useEffect(() => {
    const host = hostRef.current;
    if (!host) return undefined;
    host.innerHTML = "";

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(48, 1, 0.1, 320);
    camera.position.copy(VIEW_POINTS.WALK.camera);
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.7));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.domElement.setAttribute("aria-label", `Mô hình 3D ${model.lot.name}, tầng ${model.floorId}`);
    host.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.065;
    controls.target.copy(VIEW_POINTS.WALK.target);
    controls.maxDistance = 145;
    controls.minDistance = 1.2;
    controls.maxPolarAngle = Math.PI / 2.01;
    controls.update();

    scene.add(new THREE.AmbientLight(0xffffff, model.lot.kind === "UNDERGROUND" ? 1.0 : 1.45));
    const hemisphere = new THREE.HemisphereLight(0xccecff, 0x101820, model.lot.kind === "UNDERGROUND" ? 0.7 : 1.5);
    hemisphere.position.set(0, 60, 0);
    scene.add(hemisphere);
    const sun = new THREE.DirectionalLight(0xffffff, model.lot.kind === "UNDERGROUND" ? 1.2 : 2.0);
    sun.position.set(-28, 45, -18);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.camera.left = -70;
    sun.shadow.camera.right = 70;
    sun.shadow.camera.top = 45;
    sun.shadow.camera.bottom = -45;
    scene.add(sun);

    const sceneResult = buildGarageScene(scene, model, actor);
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const cameraTween = { active: false };
    let animationFrame = 0;
    let pointerStart = null;
    let disposed = false;

    function resize() {
      const width = Math.max(host.clientWidth, 320);
      const height = Math.max(host.clientHeight, 480);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
      renderer.setSize(width, height, false);
    }

    function transitionTo(cameraEnd, targetEnd, duration = 720) {
      cameraTween.active = true;
      cameraTween.startedAt = performance.now();
      cameraTween.duration = duration;
      cameraTween.cameraStart = camera.position.clone();
      cameraTween.targetStart = controls.target.clone();
      cameraTween.cameraEnd = cameraEnd.clone();
      cameraTween.targetEnd = targetEnd.clone();
    }

    function setView(mode) {
      const point = VIEW_POINTS[mode] || VIEW_POINTS.WALK;
      if (sceneResult.ceiling) sceneResult.ceiling.visible = mode === "WALK";
      transitionTo(point.camera, point.target);
    }

    function walkTo(target) {
      const endCamera = new THREE.Vector3(
        Math.max(-53, Math.min(53, target.x)),
        1.75,
        Math.max(-17, Math.min(17, target.z)),
      );
      const travel = endCamera.clone().sub(camera.position);
      travel.y = 0;
      if (travel.lengthSq() < 0.1) travel.set(1, 0, 0);
      travel.normalize();
      const endTarget = endCamera.clone().add(travel.multiplyScalar(9));
      endTarget.y = 1.45;
      transitionTo(endCamera, endTarget, Math.min(1250, Math.max(520, camera.position.distanceTo(endCamera) * 42)));
    }

    function updatePointer(event) {
      const rect = renderer.domElement.getBoundingClientRect();
      pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
      pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
    }

    function resolveVehicle(object) {
      let candidate = object;
      while (candidate && !candidate.userData?.vehicle) candidate = candidate.parent;
      return candidate?.userData?.vehicle || null;
    }

    function handlePointerUp(event) {
      if (!pointerStart) return;
      const moved = Math.hypot(event.clientX - pointerStart.x, event.clientY - pointerStart.y);
      pointerStart = null;
      if (moved > 7) return;
      updatePointer(event);
      raycaster.setFromCamera(pointer, camera);

      if (viewModeRef.current === "WALK") {
        const hotspotHit = raycaster.intersectObjects(sceneResult.hotspots, false)[0];
        if (hotspotHit?.object.userData.hotspot) {
          walkTo(hotspotHit.object.userData.hotspot);
          return;
        }
      }
      const vehicleHit = raycaster.intersectObjects(sceneResult.vehicleMeshes, true)[0];
      const vehicle = vehicleHit ? resolveVehicle(vehicleHit.object) : null;
      if (vehicle) {
        const vehiclePosition = new THREE.Vector3();
        vehicleHit.object.parent?.getWorldPosition(vehiclePosition);
        if (camera.position.distanceTo(vehiclePosition) <= 12) onNearVehicle?.(vehicle);
      }
    }

    function updateProximity() {
      let nearestVehicle = null;
      let nearestVehicleDistance = Infinity;
      sceneResult.vehicleMeshes.forEach((vehicleMesh) => {
        const world = new THREE.Vector3();
        vehicleMesh.getWorldPosition(world);
        const distance = camera.position.distanceTo(world);
        if (distance < nearestVehicleDistance) {
          nearestVehicleDistance = distance;
          nearestVehicle = vehicleMesh.userData.vehicle;
        }
        vehicleMesh.traverse((child) => {
          if (child.userData?.isVehicleDetail) child.visible = distance <= 11;
        });
      });
      const visibleVehicle = nearestVehicleDistance <= 11 ? nearestVehicle : null;
      if (visibleVehicle?.code !== lastVehicleRef.current?.code) {
        lastVehicleRef.current = visibleVehicle;
        onNearVehicle?.(visibleVehicle);
      }

      sceneResult.detailObjects.forEach((detail) => {
        const world = new THREE.Vector3();
        detail.parent?.getWorldPosition(world);
        detail.visible = camera.position.distanceTo(world) <= 11;
      });

      let nearElevator = false;
      sceneResult.elevatorAnchors.forEach((anchor) => {
        const world = new THREE.Vector3();
        anchor.getWorldPosition(world);
        if (camera.position.distanceTo(world) <= 9.5) nearElevator = true;
      });
      if (nearElevator !== lastElevatorRef.current) {
        lastElevatorRef.current = nearElevator;
        onElevatorProximity?.(nearElevator);
      }
    }

    function animate(now) {
      if (disposed) return;
      animationFrame = requestAnimationFrame(animate);
      if (cameraTween.active) {
        const progress = Math.min((now - cameraTween.startedAt) / cameraTween.duration, 1);
        const eased = easeInOut(progress);
        camera.position.lerpVectors(cameraTween.cameraStart, cameraTween.cameraEnd, eased);
        controls.target.lerpVectors(cameraTween.targetStart, cameraTween.targetEnd, eased);
        if (progress >= 1) cameraTween.active = false;
      }
      const walking = viewModeRef.current === "WALK";
      const direction = new THREE.Vector3();
      camera.getWorldDirection(direction);
      sceneResult.hotspots.forEach((hotspot, index) => {
        const toMarker = hotspot.position.clone().sub(camera.position);
        const distance = toMarker.length();
        toMarker.normalize();
        hotspot.visible = walking && distance > 2.3 && distance < 31 && direction.dot(toMarker) > -0.15;
        hotspot.material.opacity = 0.38 + 0.28 * Math.sin(now * 0.004 + index * 0.35);
      });
      updateProximity();
      controls.update();
      renderer.render(scene, camera);
    }

    renderer.domElement.addEventListener("pointerdown", (event) => {
      pointerStart = { x: event.clientX, y: event.clientY };
    });
    renderer.domElement.addEventListener("pointerup", handlePointerUp);
    const resizeObserver = new ResizeObserver(resize);
    resizeObserver.observe(host);
    resize();

    runtimeRef.current = { scene, camera, controls, renderer, sceneResult, setView, walkTo };
    onReady?.();
    animationFrame = requestAnimationFrame(animate);

    return () => {
      disposed = true;
      cancelAnimationFrame(animationFrame);
      resizeObserver.disconnect();
      renderer.domElement.removeEventListener("pointerup", handlePointerUp);
      controls.dispose();
      disposeGarageScene(sceneResult);
      renderer.dispose();
      renderer.domElement.remove();
      runtimeRef.current = null;
      lastVehicleRef.current = null;
      lastElevatorRef.current = false;
    };
  }, [actor, model, onElevatorProximity, onNearVehicle, onReady]);

  useEffect(() => {
    runtimeRef.current?.setView(viewMode);
  }, [viewMode]);

  return <div className="parking3d-viewport" ref={hostRef} />;
});

export default ParkingGarageViewport;
