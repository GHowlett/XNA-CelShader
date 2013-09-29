using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CelShaderExample
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        // Input tracking vars
        KeyboardState prevState;// Keyboard state on last update

        // Camera
        Matrix view;
        Matrix proj;

        // Game presentation objects and data
        Model model;            // main game object
        Texture2D m_texture;    // texture for main game object
        Matrix[] bones;         // bones for main game object
        float rotate = 0;       // rotation step
        Matrix rotation;        // rotation matrix

        Texture2D background;   // background texture

        // Debug info
        SpriteFont debugFont;   // font for debug console
        String debugMsg;        // debug message
        Vector2 debugLoc;       // position of debug message
        Color consoleFgColor = Color.DarkGreen; // Console forground color

        // CelShader effects and data
        Effect celShader;       // Toon shader effect
        Texture2D celMap;       // Texture map for cell shading
        Vector4 lightDirection; // Light source for toon shader

        Effect outlineShader;   // Outline shader effect
        float defaultThickness = 1.5f;  // default outline thickness
        float defaultThreshold = 0.2f;  // default edge detection threshold
        float outlineThickness = 1.5f;  // current outline thickness
        float outlineThreshold = 30f;  // current edge detection threshold
        float tStep = 0.01f;    // Ammount to step the line thickness by
        float hStep = 0.001f;   // Ammount to step the threshold by

        // Render target to capture cel-shaded render for edge detection
        RenderTarget2D celTarget;      

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            lightDirection = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);

            prevState = Keyboard.GetState();

            debugLoc = new Vector2(10, 10);
            debugMsg = "";

            base.Initialize();
        }

        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            view = Matrix.CreateLookAt(new Vector3(0, 10, 300), Vector3.Zero, Vector3.Up);
            proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4,
                GraphicsDevice.Viewport.AspectRatio, 1.0f, 1000.0f);

            model = Content.Load<Model>("Object");
            bones = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(bones);
            m_texture = Content.Load<Texture2D>("ColorMap");

            background = Content.Load<Texture2D>("glacier");
            debugFont = Content.Load<SpriteFont>("DebugFont");

            celShader = Content.Load<Effect>("CelShader");
            celMap = Content.Load<Texture2D>("celMap");
            celShader.Parameters["Projection"].SetValue(proj);
            celShader.Parameters["View"].SetValue(view);
            celShader.Parameters["LightDirection"].SetValue(lightDirection);
            celShader.Parameters["ColorMap"].SetValue(m_texture);
            celShader.Parameters["CelMap"].SetValue(celMap);

            outlineShader = Content.Load<Effect>("OutlineShader");
            outlineShader.Parameters["Thickness"].SetValue(outlineThickness);
            outlineShader.Parameters["Threshold"].SetValue(outlineThreshold);
            outlineShader.Parameters["ScreenSize"].SetValue(
                new Vector2(GraphicsDevice.Viewport.Bounds.Width, GraphicsDevice.Viewport.Bounds.Height));

            /* Here is the first significant difference between XNA 3.0 and XNA 4.0
             * Render targets have been significantly revamped in this version of XNA
             * this constructor creates a render target with the given width and height, 
             * no MipMap, the standard Color surface format and a depth format that provides
             * space for depth information.  The key bit here is the depth format.  If
             * we do not specify this here we will get the default DepthFormat for a render
             * target which is None.  Without a depth buffer we will not get propper culling.
             */
            celTarget = new RenderTarget2D(GraphicsDevice, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height,
                false, SurfaceFormat.Color, DepthFormat.Depth24);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit on button back or escape key press
            KeyboardState keyState = Keyboard.GetState();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || keyState.IsKeyDown(Keys.Escape))
                this.Exit();

            /* Give us a nice rotation for the main game object
             */
            rotate += (float)gameTime.ElapsedGameTime.Milliseconds / 1000;
            rotation = Matrix.CreateRotationX(rotate / 2) * Matrix.CreateRotationZ(-rotate / 4);

            /* Handle key presses to change edge thickness and the detection threshold
             * so we can experiment with different values.
             * Space    - reset both values
             * Up       - increase edge thickness
             * Down     - decrease edge thickness
             * Right    - increase edge detection threshold
             * Left     - decrease edge detection threshold
             */
            if (keyState.IsKeyDown(Keys.Up))
            {
                outlineThickness += tStep;
            }
            if (keyState.IsKeyDown(Keys.Down))
            {
                outlineThickness -= tStep;
            }

            if (keyState.IsKeyDown(Keys.Right))
            {
                outlineThreshold += hStep;
            }
            if (keyState.IsKeyDown(Keys.Left))
            {
                outlineThreshold -= hStep;
            }

            if (keyState.IsKeyDown(Keys.Space))
            {
                outlineThickness = defaultThickness;
                outlineThreshold = defaultThreshold;
            }

            /* Make sure our thickness and threshold are nice even values
             * (Not strictly needed but makes it prettier)
             */
            outlineThickness = (float)Math.Round(MathHelper.Clamp(outlineThickness, 0.0f, 10.0f),2);
            outlineThreshold = (float)Math.Round(MathHelper.Clamp(outlineThreshold, 0.0f, 1.0f), 3);

            /* Update the debug console message to display the thickness T and
             * and threshold H
             */
            debugMsg = "T: " + outlineThickness + "\nH: " + outlineThreshold;

            /* Update the outline shader with the new thickness and
             * threshold parameters
             */
            outlineShader.Parameters["Thickness"].SetValue(outlineThickness);
            outlineShader.Parameters["Threshold"].SetValue(outlineThreshold);

            /* update our previous keystate
             */
            prevState = keyState;
            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            /* Set the render target to celTarget this will cause everything
             * that we draw to the graphics device to go here instead of to the
             * screen.  We will do this to capture the render of the game object
             * so we can do post processing edge detection.
             * 
             * For XNA 4.0 we do not need to worry about render target indexes
             * any more, just set the render target.
             */
            GraphicsDevice.SetRenderTarget(celTarget);

            /* Here is one of the big changes needed to get this working right
             * in XNA 4.0.  We need to set out GraphicsDevice.DepthStencilState
             * to default (this isn't done for us on a render target) otherwise
             * culling will be messed up.
             * 
             * An important thing to remember here in XNA 4.0 setting a state
             * is really cheep, creating a state is expensive.  The current
             * operations are just fine to call on every draw (infact that is
             * what Microsoft would recommend).  Creating new state objects on
             * the otherhand would not be a good idea.  Either use the built in
             * states like I am doing here, create your states somewhere else
             * and reuse them, or work up some mechanism to cache states for reuse.
             */
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            /* Setting the other states isn't really necessary but good form
             */
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            /* We are really only interested in the model so clear the graphics
             * device with a transparent (alpha = 0) color.
             */
            Color alpha = Color.White;
            alpha.A = 0;
            GraphicsDevice.Clear(alpha);

            /* Finally we get to draw the game model
             * 
             * A model may have more than one mesh... so for each one of those
             */
            foreach (ModelMesh mesh in model.Meshes)
            {
                /* Our world matrix is the bone transformation
                 * for the current model's parent bone
                 */
                Matrix world = bones[mesh.ParentBone.Index];

                /* These are the only two effect parameters that are mesh dependent
                 * world and inverse world so set those here
                 */
                celShader.Parameters["World"].SetValue(world * rotation);
                celShader.Parameters["InverseWorld"].SetValue(Matrix.Invert(world * rotation));

                /* Now, for each part of the current mesh...
                 */
                foreach (ModelMeshPart meshPart in mesh.MeshParts)
                {
                    /* Set the vertex buffer and vertex indicies
                     */
                    GraphicsDevice.SetVertexBuffer(meshPart.VertexBuffer,
                            meshPart.VertexOffset);
                    GraphicsDevice.Indices = meshPart.IndexBuffer;

                    /* Set the current technique in the celShader effect.
                     * This isn't strictly necessary CurrentTechnique defaults to
                     * the first technique defined in the effect file and since
                     * CelShader only has the one ToonShader technique we would 
                     * have been just fine without it.
                     * 
                     * But since it doesn't hurt to be explicit and it demonstrates
                     * selecting an effect technique...
                     */
                    celShader.CurrentTechnique = celShader.Techniques["ToonShader"];

                    /* Now for each pass in the current technique
                     * (again not needed here because the ToonShader only has a single
                     * pass but its nice to have more generalized code)
                     */
                    foreach (EffectPass effectPass in celShader.CurrentTechnique.Passes)
                    {
                        /* XNA 4.0 gets rid of effect begins and ends... just apply the
                         * effect now
                         */
                        effectPass.Apply();

                        /* And draw
                         */
                        GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0,
                            meshPart.NumVertices, meshPart.StartIndex, meshPart.PrimitiveCount);
                    }
                }

            }

            /* We are done with the render target so set it back to null.
             * This will get us back to rendering to the default render target
             */
            GraphicsDevice.SetRenderTarget(null);

            /* Clear the device to get ready for more drawing
             */
            GraphicsDevice.Clear(Color.Wheat);

            /* Draw our background and debug message
             */
            spriteBatch.Begin();
            spriteBatch.Draw(background, Vector2.Zero, Color.White);
            spriteBatch.DrawString(debugFont, debugMsg, debugLoc, consoleFgColor);
            spriteBatch.End();

            /* Also in XNA 4.0 applying effects to a sprite is a little different
             * Use an overload of Begin that takes the effect as a parameter.  Also make
             * sure to set the sprite batch blend state to Opaque or we will not get black
             * outlines.
             */
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, null, outlineShader);
            spriteBatch.Draw(celTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
