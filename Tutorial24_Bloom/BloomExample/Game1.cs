using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace BloomExample
{
    public struct VertexPositionNormalTextureTangentBinormal
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;
        public Vector3 Tangent;
        public Vector3 Binormal;

        public static readonly VertexElement[] VertexElements =
        new VertexElement[]
        {
            new VertexElement(0, 0, VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Position, 0),
            new VertexElement(0, sizeof(float) * 3, VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Normal, 0),
            new VertexElement(0, sizeof(float) * 6, VertexElementFormat.Vector2, VertexElementMethod.Default, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(0, sizeof(float) * 8, VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Tangent, 0),
            new VertexElement(0, sizeof(float) * 11, VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Binormal, 0),
        };

        public VertexPositionNormalTextureTangentBinormal(Vector3 position, Vector3 normal, Vector2 textureCoordinate, Vector3 tangent, Vector3 binormal)
        {
            Position = position;
            Normal = normal;
            TextureCoordinate = textureCoordinate;
            Tangent = tangent;
            Binormal = binormal;
        }

        public static int SizeInBytes { get { return sizeof(float) * 14; } }
    }

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class ShaderTutorial : Microsoft.Xna.Framework.Game
    {
        // Variables for Matrix calculations, viewport and object movment
        float width, height;
        float x = 0, y = 0;
        float zHeight = 15.0f;
        float moveObject = 0;

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        // 3D Object
        Model m_Model;
        Model m_ModelA;

        // Our effect object, this is where our shader will be loaded abd compiled
        Effect effect;
        Effect effectBlur;
        Effect effectBloom;
        Effect effectCombine;

        // Render target
        RenderTarget2D renderTarget;
        RenderTarget2D renderTargetBloom;
        RenderTarget2D renderTargetBlurBloom;
        RenderTarget2D renderTargetBlurIIBloom;
        Texture2D SceneTexture;
        Texture2D BloomTexture;
        Texture2D BlurBloomTexture;
        Texture2D BlurIIBloomTexture;

        // Textures
        Texture2D colorMap;
        Texture2D normalMap;
        Texture2D glossMap;
        Texture2D alphaMap;

        // Matrices
        Matrix renderMatrix, objectMatrix, worldMatrix, viewMatrix, projMatrix;
        Matrix[] bones;




        // Constructor
        public ShaderTutorial()
        {
            Window.Title = "XNA Shader Programming Tutorial 24 - Bloom post process";
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            m_Model = null;
            m_ModelA = null;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            width = graphics.GraphicsDevice.Viewport.Width;
            height = graphics.GraphicsDevice.Viewport.Height;

            // Set worldMatrix to Identity
            worldMatrix = Matrix.Identity;

            float aspectRatio = (float)width / (float)height;
            float FieldOfView = (float)Math.PI / 2, NearPlane = 1.0f, FarPlane = 1000.0f;
            projMatrix = Matrix.CreatePerspectiveFieldOfView(FieldOfView, aspectRatio, NearPlane, FarPlane);

            // Load and compile our Shader into our Effect instance.
            effect = Content.Load<Effect>("Shader");
            effectBlur = Content.Load<Effect>("Blur");
            effectBloom = Content.Load<Effect>("Bloom");
            effectCombine = Content.Load<Effect>("BloomCombine");

            // Load textures
            colorMap = Content.Load<Texture2D>("model_diff");
            normalMap = Content.Load<Texture2D>("model_norm");
            glossMap = Content.Load<Texture2D>("model_spec");
            alphaMap = Content.Load<Texture2D>("model_alpha");

            // Vertex declaration for rendering our 3D model.
            graphics.GraphicsDevice.VertexDeclaration = new VertexDeclaration(graphics.GraphicsDevice, VertexPositionNormalTextureTangentBinormal.VertexElements);
            graphics.GraphicsDevice.RenderState.CullMode = CullMode.None;


            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load our 3D model and transform bones.
            m_Model = Content.Load<Model>("Object");
            m_ModelA = Content.Load<Model>("ObjectA");
            bones = new Matrix[this.m_Model.Bones.Count];
            this.m_Model.CopyAbsoluteBoneTransformsTo(bones);

            PresentationParameters pp = graphics.GraphicsDevice.PresentationParameters;
            renderTarget = new RenderTarget2D(graphics.GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, 1, graphics.GraphicsDevice.DisplayMode.Format);
            renderTargetBloom = new RenderTarget2D(graphics.GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, 1, graphics.GraphicsDevice.DisplayMode.Format);
            renderTargetBlurBloom = new RenderTarget2D(graphics.GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, 1, graphics.GraphicsDevice.DisplayMode.Format);
            renderTargetBlurIIBloom = new RenderTarget2D(graphics.GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight, 1, graphics.GraphicsDevice.DisplayMode.Format);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            zHeight = 30;

            float m = (float)gameTime.ElapsedGameTime.Milliseconds / 1000;
            moveObject += m;

            // Move our object by doing some simple matrix calculations.
            objectMatrix = Matrix.CreateRotationX(0) * Matrix.CreateRotationY(-moveObject / 4);
            renderMatrix = Matrix.CreateScale(0.5f);
            viewMatrix = Matrix.CreateLookAt(new Vector3(x, y, zHeight), new Vector3(0, y, 0), Vector3.Up);

            renderMatrix = objectMatrix * renderMatrix;

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            ////////////////////////////////////////
            // Render the normal scene to a texture
            graphics.GraphicsDevice.SetRenderTarget(0, renderTarget);
            graphics.GraphicsDevice.Clear(Color.Black);


            // Use the AmbientLight technique from Shader.fx. You can have multiple techniques in a effect file. If you don't specify
            // what technique you want to use, it will choose the first one by default.
            effect.CurrentTechnique = effect.Techniques["GlossMap"];

            // Begin our effect
            effect.Begin();


            // A shader can have multiple passes, be sure to loop trough each of them.
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                // Begin current pass
                pass.Begin();

                foreach (ModelMesh mesh in m_Model.Meshes)
                {
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        // calculate our worldMatrix..
                        worldMatrix = bones[mesh.ParentBone.Index] * renderMatrix;

                        Vector4 vecEye = new Vector4(x, y, zHeight, 0);


                        // .. and pass it into our shader.
                        // To access a parameter defined in our shader file ( Shader.fx ), use effectObject.Parameters["variableName"]
                        Matrix worldInverse = Matrix.Invert(worldMatrix);
                        Vector4 vLightDirection = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
                        effect.Parameters["matWorldViewProj"].SetValue(worldMatrix * viewMatrix * projMatrix);
                        effect.Parameters["matWorld"].SetValue(worldMatrix);
                        effect.Parameters["vecEye"].SetValue(vecEye);
                        effect.Parameters["vecLightDir"].SetValue(vLightDirection);
                        effect.Parameters["ColorMap"].SetValue(colorMap);
                        effect.Parameters["NormalMap"].SetValue(normalMap);
                        effect.Parameters["GlossMap"].SetValue(glossMap);
                        effect.Parameters["AlphaMap"].SetValue(alphaMap);
                        
                        

                        // Render our meshpart
                        graphics.GraphicsDevice.Vertices[0].SetSource(mesh.VertexBuffer, part.StreamOffset, part.VertexStride);
                        graphics.GraphicsDevice.Indices = mesh.IndexBuffer;
                        graphics.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                                                                      part.BaseVertex, 0, part.NumVertices,
                                                                      part.StartIndex, part.PrimitiveCount);
                    }
                }

                // Stop current pass
                pass.End();
            }
            // Stop using this effect
            effect.End();


            graphics.GraphicsDevice.SetRenderTarget(0, null);
            SceneTexture = renderTarget.GetTexture();



            ////////////////////////////////////////
            // Render the bright areas of the scene in SceneTexture to a new texture
            graphics.GraphicsDevice.SetRenderTarget(0, renderTargetBloom);
            graphics.GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteBlendMode.None, SpriteSortMode.Immediate, SaveStateMode.SaveState);
            {
                // Apply the post process shader
                effectBloom.Begin();
                {
                    effectBloom.CurrentTechnique.Passes[0].Begin();
                    {
                        spriteBatch.Draw(SceneTexture, new Rectangle(0, 0, 800, 600), Color.White);
                        effectBloom.CurrentTechnique.Passes[0].End();
                    }
                }
                effectBloom.End();
            }
            spriteBatch.End();

            graphics.GraphicsDevice.SetRenderTarget(0, null);
            BloomTexture = renderTargetBloom.GetTexture();


            ////////////////////////////////////////
            // Blur the bright areas in the BloomTexture, making them "glow"
            graphics.GraphicsDevice.SetRenderTarget(0, renderTargetBlurBloom);
            graphics.GraphicsDevice.Clear(Color.Black);
            spriteBatch.Begin(SpriteBlendMode.None, SpriteSortMode.Immediate, SaveStateMode.SaveState);
            {
                // Apply the post process shader
                effectBlur.Begin();
                {
                    effectBlur.CurrentTechnique.Passes[0].Begin();
                    {
                        spriteBatch.Draw(BloomTexture, new Rectangle(0, 0, 800, 600), Color.White);
                        effectBlur.CurrentTechnique.Passes[0].End();
                    }
                }
                effectBlur.End();
            }
            spriteBatch.End();
            graphics.GraphicsDevice.SetRenderTarget(0, null);
            BlurBloomTexture = renderTargetBlurBloom.GetTexture();

            ////////////////////////////////////////
            // Blur the bright areas in the BloomTexture a 2nd time, making them "glow" even more
            graphics.GraphicsDevice.SetRenderTarget(0, renderTargetBlurIIBloom);
            graphics.GraphicsDevice.Clear(Color.Black);
            spriteBatch.Begin(SpriteBlendMode.None, SpriteSortMode.Immediate, SaveStateMode.SaveState);
            {
                // Apply the post process shader
                effectBlur.Begin();
                {
                    effectBlur.CurrentTechnique.Passes[0].Begin();
                    {
                        spriteBatch.Draw(BlurBloomTexture, new Rectangle(0, 0, 800, 600), Color.White);
                        effectBlur.CurrentTechnique.Passes[0].End();
                    }
                }
                effectBlur.End();
            }
            spriteBatch.End();
            graphics.GraphicsDevice.SetRenderTarget(0, null);
            BlurIIBloomTexture = renderTargetBlurIIBloom.GetTexture();


            graphics.GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.DarkSlateBlue, 1.0f, 0);

            spriteBatch.Begin(SpriteBlendMode.None, SpriteSortMode.Immediate, SaveStateMode.SaveState);
            {
                // Apply the post process shader
                effectCombine.Begin();
                {
                    effectCombine.CurrentTechnique.Passes[0].Begin();
                    {
                        effectCombine.Parameters["ColorMap"].SetValue(SceneTexture);
                        spriteBatch.Draw(BlurIIBloomTexture, new Rectangle(0, 0, 800, 600), Color.White);
                        effectCombine.CurrentTechnique.Passes[0].End();
                    }
                }
                effectCombine.End();
            }
            spriteBatch.End();







            base.Draw(gameTime);
        }
    }
}
