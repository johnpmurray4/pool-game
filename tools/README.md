# tools/

Development tooling that never ships:

- **Visualizer/** — renders simulated shots as animated SVGs (table, exact
  ball paths, pot markers, looping ball animation). The main instrument for
  eyeballing physics behaviour and, eventually, tuning against real UK pool
  footage.

  ```sh
  cd tools/Visualizer
  dotnet run all out/          # break, pot, draw, sidespin scenarios
  dotnet run draw .            # a single scenario, into the current dir
  ```

  Open the `.svg` files in any browser; the balls animate on a loop.

- **Tuning harness** (planned): batch-runs canned shots across parameter
  sweeps of `PhysicsConfig` and reports trajectory differences.
