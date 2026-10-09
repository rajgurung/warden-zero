// Single entry point for Babylon.js. Deep imports keep the bundle to the
// parts we use (the '@babylonjs/core' barrel pulls in the whole engine);
// the side-effect imports register scene components those classes need.
import '@babylonjs/core/Lights/Shadows/shadowGeneratorSceneComponent';
import '@babylonjs/core/Layers/effectLayerSceneComponent';
import '@babylonjs/core/PostProcesses/RenderPipeline/postProcessRenderPipelineManagerSceneComponent';
import '@babylonjs/core/Particles/particleSystemComponent';
import '@babylonjs/core/Meshes/instancedMesh';
import '@babylonjs/core/Culling/ray';

export { Engine } from '@babylonjs/core/Engines/engine';
export { Scene } from '@babylonjs/core/scene';
export { FreeCamera } from '@babylonjs/core/Cameras/freeCamera';
export { Vector3, Matrix } from '@babylonjs/core/Maths/math.vector';
export { Color3, Color4 } from '@babylonjs/core/Maths/math.color';
export { HemisphericLight } from '@babylonjs/core/Lights/hemisphericLight';
export { DirectionalLight } from '@babylonjs/core/Lights/directionalLight';
export { PointLight } from '@babylonjs/core/Lights/pointLight';
export { ShadowGenerator } from '@babylonjs/core/Lights/Shadows/shadowGenerator';
export { GlowLayer } from '@babylonjs/core/Layers/glowLayer';
export { DefaultRenderingPipeline } from '@babylonjs/core/PostProcesses/RenderPipeline/Pipelines/defaultRenderingPipeline';
export { ImageProcessingConfiguration } from '@babylonjs/core/Materials/imageProcessingConfiguration';
export { MeshBuilder } from '@babylonjs/core/Meshes/meshBuilder';
export { StandardMaterial } from '@babylonjs/core/Materials/standardMaterial';
export { DynamicTexture } from '@babylonjs/core/Materials/Textures/dynamicTexture';
export { Texture } from '@babylonjs/core/Materials/Textures/texture';
export { Mesh } from '@babylonjs/core/Meshes/mesh';
export { TransformNode } from '@babylonjs/core/Meshes/transformNode';
export { ParticleSystem } from '@babylonjs/core/Particles/particleSystem';
export type { InstancedMesh } from '@babylonjs/core/Meshes/instancedMesh';
export type { AbstractMesh } from '@babylonjs/core/Meshes/abstractMesh';
