A comprehensive Model Context Protocol (MCP) server

An MCP server that drives a running SOLIDWORKS session over its COM API. https://help.solidworks.com/2024/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks_namespace.html

Calls the documented API — so if a tool can't do something, neither could a macro.

92 tools: sketching with real relations and driving dimensions, the solid features you actually reach for, reference geometry, assemblies and mates, and — importantly — a feedback channel, including screenshots returned as images so the model can see what it just built.

https://github.com/andrewbartels1/SolidworksMCP-python

https://github.com/Eduardof0nt/Solidworks-MCP-Server

https://github.com/Slacker-LLC/solidworks-mcp  187 tools spanning every major workflow — modeling, sketching, assemblies, drawings, sheet metal, weldments, mold tools, surfaces, simulation, motion, rendering, materials, and more.
"Create a 100×60×40 mm aluminum part, render it, and export to STEP"
"Open this assembly, check for interferences, then mass-analyze each component"
"Generate a 3-view drawing of bracket.sldprt with dimensions, GD&T, and a BOM, then save as PDF"
"Design a parametric sheet-metal enclosure with 2 mm thickness, four edge flanges, and export flat patterns"
"Build a weldment frame from 40×40×4 square tube along this sketch"
"Set up a static simulation, apply this fixture and 500 N load, and run the analysis"

The two ideas that make it usable
1. Units are explicit, always
Every length parameter ends in _mm and every angle in _deg. The COM API underneath is metres and radians; conversion happens once, at the boundary. No tool has ever silently taken metres.

2. Selection is declarative
SOLIDWORKS features read from an implicit global selection set, with per-feature "marks" deciding which selection means what. Exposing that to an agent is hopeless. Instead every tool that needs geometry takes the same selection object, and the server sets the marks:

// list_edges first, then:
{ "selection": { "edges": [4, 6, 9] } }
{ "selection": { "face_edges": [2] } }        // every edge of face 2
{ "selection": { "planes": ["front"] } }
{ "selection": { "sketch_segments": [0, 1] } }
{ "selection": { "points": [{ "x_mm": 30, "y_mm": 0, "z_mm": 20, "type": "FACE" }] } }
Indices come from list_faces / list_edges / list_vertices / list_sketch_segments and describe the current model state — re-list after any geometry change. Under the hood, selection resolves to a point that provably lies on the entity and picks by 3D coordinate, which survives the fact that late-bound Python cannot QueryInterface a Face2 to IEntity.

Tools
Session and documents
Tool	Purpose
solidworks_status	Verify the connection; report version and active document.
get_active_document_info	Title, path, type, unsaved state.
create_new_document	New part / assembly / drawing from the default template.
open_document	Open a .sldprt / .sldasm / .slddrw.
save_document / save_active_document	Save-as under the output root / save in place.
export_document	STEP, IGES, STL, Parasolid, 3MF, or an image.
rebuild_document	Rebuild and report failing features.
set_appearance / set_material	Display colour; real material (so mass properties mean something).
Reference geometry
list_reference_planes, create_plane (offset / angle / midplane / three points / parallel-through-point), create_axis.

Sketching
create_sketch (on a plane or a model face), edit_sketch, close_sketch, list_sketches, list_sketch_segments, get_sketch_status.

Geometry: draw_line, draw_centerline, draw_circle, draw_rectangle, draw_arc, draw_3point_arc, draw_ellipse, draw_polygon, draw_slot, draw_point, draw_spline.

Editing: sketch_fillet, sketch_chamfer, sketch_trim, sketch_offset, sketch_mirror, convert_entities, set_construction_geometry.

Parametrics: add_relation, add_dimension, set_dimension, list_dimensions.

Features
boss_extrude, cut_extrude, revolve, fillet, chamfer, shell, draft, rib, simple_hole, sweep, loft, linear_pattern, circular_pattern, mirror_feature, delete_feature, rename_feature, set_feature_suppression.

Inspection — the feedback channel
Tool	Purpose
capture_screenshot	Returns the view as an image, so the model can look at its own work.
list_faces / list_edges / list_vertices / list_bodies	Topology with types, sizes, and selection indices. Filterable by surface type, area, normal direction, curve type, length.
get_mass_properties / get_bounding_box / measure	Numbers to check the geometry against.
check_errors	What SOLIDWORKS thinks is wrong — see below.
set_view	Named view plus zoom-to-fit.
Assemblies
list_components, insert_component, add_mate, list_mates, set_component_fixed.

Engineering drawings
create_drawing, list_sheets, add_sheet, activate_sheet, insert_standard_views, insert_model_view, insert_projected_view, insert_section_view, insert_detail_view, list_drawing_views, activate_drawing_view, create_drawing_sketch, set_drawing_view, insert_model_annotations, auto_dimension_view, insert_center_marks, insert_centerlines, add_note.

For section and detail views, call create_drawing_sketch after activating the parent view, then use the normal draw_line or draw_circle sketch tools.

A worked example
create_new_document   { "kind": "part" }
create_sketch         { "plane": "front", "name": "Base" }
draw_rectangle        { "x1_mm": 10, "y1_mm": 10, "x2_mm": 70, "y2_mm": 50 }
add_dimension         { "selection": { "sketch_segments": [0] }, "kind": "horizontal",
                        "value_mm": 80, "place_x_mm": 40, "place_y_mm": -10 }
add_dimension         { "selection": { "sketch_segments": [1] }, "kind": "vertical",
                        "value_mm": 50, "place_x_mm": -10, "place_y_mm": 30 }
close_sketch          {}
boss_extrude          { "depth_mm": 20, "name": "BasePad" }

list_edges            { "curve_type": "line", "min_length_mm": 19 }   // find the four verticals
fillet                { "radius_mm": 5, "selection": { "edges": [0, 1, 2, 3] } }
shell                 { "thickness_mm": 2, "selection": { "faces": [4] } }

capture_screenshot    { "view": "isometric" }
get_mass_properties   {}
Notes from the implementation
These are the things that cost real time. They are documented because anyone automating SOLIDWORKS from Python will hit them.

SOLIDWORKS members are inconsistently exposed to pywin32. Most are declared in the type library as PROPGET with arguments. Late-bound pywin32 may resolve such a member on plain attribute access by invoking it with no arguments. IBody2.GetFaces then returns a bound method that looks like a one-element result — which is why an extruded box reported exactly one face. Worse, ModelDocExtension.AddDimension invoked that way crashes SOLIDWORKS outright. sw_core.flag_methods() calls _FlagAsMethod on every member the server invokes with arguments, and sw_core.value() invokes zero-argument callables rather than returning the method object.

Modal dialogs deadlock the server. AddDimension2 pops the "Modify" box whenever Input dimension value is enabled — which is the default. A modal dialog blocks the COM call that opened it, and with it the whole server, until somebody clicks. Dimension tools turn the preference off and restore it after. If a tool ever hangs, look for a dialog behind the SOLIDWORKS window.

Return values lie. SketchTrim returns False on success. The Create* sketch APIs return arrays whose truthiness is unreliable. SketchAddConstraints takes a magic string and silently ignores one it does not recognise. So the drawing tools judge success by the sketch's segment count, sketch_trim by whether the sketch actually changed, and add_relation uses the typed ISketchRelationManager.AddRelation and verifies via the relation count — and on failure reports which relations that selection would accept.

Most failures are silent. SOLIDWORKS returns null far more often than it raises. Every feature tool therefore rebuilds and reports ModelDocExtension.GetWhatsWrong, and check_errors exposes it directly.

sketch_trim needs its target selected. Pass the segment in selection; the point alone only tells SOLIDWORKS which piece to discard.

Multi-reference features want distinct marks. InsertRefPlane reads its first, second, and third reference from marks 0, 1, 2 — not from selection order. Selecting all of them with mark 0 silently produces nothing, which is why create_plane selects each reference with its own mark and documents that references are consumed in written order.

Two unit traps. IPartDoc.GetPartBox(False) returns document units, not metres, so it reads 1000x large if you treat it like the rest of the API; pass True. And swSketchLINE is 0, so folding a segment type through or turns every line into "unknown".

Known limitations
One SOLIDWORKS session, one COM apartment: tool calls are serialised. Two clients driving the same session at once will deadlock it.
Snapshot indices from list_faces / list_edges are invalidated by any geometry change. Re-list; do not cache.
A modal dialog opened by SOLIDWORKS for any other reason will still block the server until dismissed.
rib is picky about its profile. InsertRib returns void and simply builds nothing unless the open profile reaches material at both ends and the extrusion direction can get there. The tool defaults to parallel_to_sketch (right for a cross-section profile) and retries the other direction automatically, reporting which one worked. A profile that overlaps an existing rib still fails, and the tool says so rather than pretending.
Only the first insert_projected_view off a given parent succeeds; subsequent projections from the same parent return nothing, through re-activation and rebuild alike. Use insert_standard_views for a full orthographic set.
Detail-view scale is absolute (model to paper), not relative to the parent. On a 1:4 sheet, 2:1 makes the detail eight times the parent view and it overflows the sheet. Pick a scale near the sheet scale.
Standard-plane and model-view display names are localized. Use front, top, and right for reference planes, and use names returned by list_drawing_views for drawing views; do not hard-code English UI labels.
through_all_both is translated internally to a two-direction feature with through_all in both directions. Passing swEndCondThroughAllBoth as a single-ended feature silently cuts only one side in SOLIDWORKS 2026.
circular_pattern cannot pattern a feature that built a separate body (merge: false) — SOLIDWORKS rejects the feature scope. Merge the body, or pattern the body instead.
save_document cannot overwrite a file SOLIDWORKS currently has open, even with overwrite: true. The error says so when that is the cause.



__________

Tools Reference
Connection
Tool	Description
sw_connect	Connect to SolidWorks (launches if needed)
sw_get_status	Connection status + active document info
Files & Documents
Tool	Description
sw_open_document	Open .sldprt, .sldasm, .slddrw, .step, .stl, etc.
sw_close_document	Close a document
sw_save_document	Save / Save As
sw_create_part	New part document
sw_create_assembly	New assembly document
sw_create_drawing	New drawing document
sw_list_open_documents	List all open documents
sw_export_file	Export to STEP, STL, DXF, PDF, IGES, etc.
sw_get_model_info	Full model info + feature tree
Selection
Tool	Description
sw_select_entity	Select by name and type
sw_clear_selection	Clear selection
sw_list_features	List all FeatureManager features
Sketch
Tool	Description
sw_create_sketch	Start sketch on plane/face
sw_edit_sketch	Open existing sketch
sw_exit_sketch	Exit sketch mode
sw_sketch_line	Draw line
sw_sketch_circle	Draw circle
sw_sketch_rectangle	Draw rectangle
sw_sketch_arc	Draw arc
sw_sketch_ellipse	Draw ellipse
sw_sketch_polygon	Draw regular polygon
sw_sketch_spline	Draw spline
sw_sketch_centerline	Draw centerline
sw_sketch_offset	Offset selected entities
sw_sketch_linear_pattern	Linear step-and-repeat
sw_sketch_circular_pattern	Circular step-and-repeat
sw_add_sketch_dimension	Add smart dimension
sw_add_sketch_constraint	Add geometric constraint
Features (Part)
Tool	Description
sw_extrude	Extrude boss or cut
sw_revolve	Revolve boss or cut
sw_fillet	Add constant fillet
sw_chamfer	Add chamfer
sw_shell	Shell body
sw_draft	Add draft angle
sw_linear_pattern	Linear feature pattern
sw_circular_pattern	Circular feature pattern
sw_mirror	Mirror feature
sw_hole_wizard	Hole Wizard (threaded/clearance holes)
sw_loft	Loft between profiles
sw_sweep	Sweep profile along path
sw_delete_feature	Delete feature
sw_suppress_feature	Suppress feature
sw_unsuppress_feature	Unsuppress feature
Reference Geometry
Tool	Description
sw_create_reference_plane	Offset reference plane
sw_create_reference_axis	Reference axis
sw_create_reference_point	Reference point
Surface Modeling
Tool	Description
sw_thicken_surface	Thicken surface to solid
sw_knit_surfaces	Knit surfaces together
Assembly
Tool	Description
sw_insert_component	Insert part/subassembly
sw_add_mate	Add constraint mate
sw_fix_component	Fix/float component
sw_check_interference	Interference detection
sw_explode_assembly	Exploded view
Drawing
Tool	Description
sw_add_drawing_view	Add model view
sw_add_section_view	Add section view
sw_add_drawing_dimension	Add dimension
sw_add_annotation	Add text note
sw_add_bom_table	Add Bill of Materials
Dimensions & Parameters
Tool	Description
sw_get_dimension	Read dimension value
sw_set_dimension	Set dimension value (parametric drive)
Analysis
Tool	Description
sw_get_mass_properties	Mass, volume, center of gravity
sw_get_bounding_box	Envelope dimensions
sw_measure_distance	Distance between entities
sw_check_geometry	Geometry validity check
sw_analyze_draft	Draft angle analysis
Properties & Configurations
Tool	Description
sw_get_custom_properties	Read custom properties
sw_set_custom_property	Write custom property
sw_list_configurations	List configurations
sw_activate_configuration	Switch configuration
sw_add_configuration	Add new configuration
Equations
Tool	Description
sw_get_equations	List all equations
sw_add_equation	Add equation/global variable
sw_delete_equation	Delete equation
sw_create_design_table	Create design table
Visualization
Tool	Description
sw_capture_screenshot	Screenshot of model view
sw_set_view_orientation	Set view (front/iso/etc.)
sw_zoom_to_fit	Zoom to fit
sw_set_display_mode	Wireframe/shaded/etc.
sw_rebuild_model	Force rebuild
Macros & Automation
Tool	Description
sw_run_macro	Run .swp/.swb macro file
sw_run_vba_code	Execute VBA code directly
sw_start_macro_recording	Record macro
sw_stop_macro_recording	Stop recording
Sheet Metal — Basic
Tool	Description
sw_flatten_sheet_metal	Toggle flat pattern view
sw_get_flat_pattern_info	Flat pattern dimensions
sw_export_flat_pattern	Export flat pattern DXF/PDF
Extended Toolset (additional 99 tools)
Materials & Appearance
Tool	Description
sw_set_material	Apply material from SolidWorks database
sw_get_material	Read current material
sw_set_appearance_color	Set RGB color and transparency
sw_list_materials	List material databases
Body Operations
Tool	Description
sw_list_bodies	List solid/surface bodies
sw_combine_bodies	Union / Subtract / Intersect bodies
sw_split_body	Split body with trim tool
sw_move_body	Translate/rotate (or copy) selected body
sw_delete_body	Delete selected body
sw_scale_body	Scale body uniformly
Curves & 3D Sketch
Tool	Description
sw_create_helix	Helix for threads, springs
sw_start_3d_sketch	Enter 3D sketch mode
sw_split_line	Split a face with a sketch
sw_projected_curve	Project sketch onto face
sw_composite_curve	Combine edges into single curve
Advanced Sketch
Tool	Description
sw_sketch_convert_entities	Convert edges → sketch entities
sw_sketch_trim	Power-trim selected entity
sw_sketch_extend	Extend selected entity
sw_sketch_construction	Toggle construction geometry
sw_sketch_fillet	Fillet corner
sw_sketch_chamfer	Chamfer corner
sw_sketch_text	Insert text for engraving
Surface Modeling Extended
Tool	Description
sw_planar_surface	Planar surface from sketch/edges
sw_extruded_surface	Extruded surface
sw_revolved_surface	Revolved surface
sw_offset_surface	Offset face/surface
sw_trim_surface	Trim surface
sw_extend_surface	Extend surface
sw_delete_face	Delete face (with optional patch)
sw_filled_surface	Fill closed edge boundary
Sheet Metal — Complete
Tool	Description
sw_base_flange	Convert sketch → sheet metal base
sw_edge_flange	Add edge flange
sw_miter_flange	Add miter flange
sw_sketched_bend	Sketched bend on flat face
sw_jog	Jog bend
sw_hem	Closed/open/teardrop/rolled hem
sw_unfold	Unfold bends to flatten
sw_fold	Re-fold bends
Weldments
Tool	Description
sw_insert_weldment	Enable weldment feature
sw_add_structural_member	Add structural member (tube, pipe, angle)
sw_add_gusset	Add triangular gusset
sw_add_end_cap	Add end cap on member face
sw_trim_weldment	Trim members to trim tool
Mold Tools
Tool	Description
sw_parting_line	Parting line for mold
sw_shut_off_surface	Auto shut-off surfaces
sw_tooling_split	Split mold block to core/cavity
Drawing — Extended
Tool	Description
sw_add_projected_view	Projected view from parent
sw_add_auxiliary_view	Auxiliary view normal to edge
sw_add_detail_view	Detail view of region
sw_add_broken_view	Broken view (compressed)
sw_add_centerline	Auto centerlines
sw_add_center_mark	Auto center marks
sw_add_balloon	BOM balloon
sw_add_surface_finish	Surface finish symbol
sw_add_geometric_tolerance	GD&T frame
sw_add_weld_symbol	Welding symbol
sw_add_datum_feature	Datum feature symbol
sw_add_hole_table	Hole table
sw_add_revision_table	Revision table
sw_export_drawing_pdf	Export drawing → PDF
sw_export_drawing_dwg	Export drawing → DWG/DXF
Assembly — Extended
Tool	Description
sw_replace_component	Swap component with different file
sw_pattern_component_linear	Linear component pattern
sw_pattern_component_circular	Circular component pattern
sw_mirror_component	Mirror component about plane
sw_suppress_component	Suppress/unsuppress
sw_hide_component	Hide/show component
sw_move_component	Translate component
sw_get_assembly_tree	List all components with state
Display & Visualization
Tool	Description
sw_create_section_view	Live model section view
sw_set_background	Viewport background style
sw_take_photoview_screenshot	PhotoView 360 render
sw_hide_all_planes	Hide all reference planes
sw_show_all_planes	Show all reference planes
sw_hide_all_sketches	Hide all sketches
sw_show_all_sketches	Show all sketches
sw_view_rotate	Rotate view by Euler angles
Motion & Simulation
Tool	Description
sw_create_motion_study	Create motion study
sw_create_simulation_study	Create simulation study (static, frequency, buckling, thermal, drop, fatigue)
sw_run_simulation	Run active simulation
Global Variables & Parametrics
Tool	Description
sw_list_global_variables	List globals
sw_set_global_variable	Set/create a global variable
sw_link_dimension_to_equation	Link dimension to expression
Display States & Sensors
Tool	Description
sw_list_display_states	List display states
sw_activate_display_state	Activate display state
sw_list_sensors	List sensors with values
File Management Extended
Tool	Description
sw_save_all	Save all open documents
sw_close_all	Close all open documents
sw_save_as_copy	Save as copy (keeps current open)
sw_get_referenced_documents	List referenced files
Advanced Features
Tool	Description
sw_rib	Rib feature from sketch
sw_dome	Dome on planar face
sw_wrap	Wrap sketch on curved face (emboss/deboss/scribe)
sw_intersect	Intersect bodies/surfaces
sw_boundary_boss	Boundary boss/cut
sw_fill_pattern	Fill face with seed pattern
sw_curve_driven_pattern	Pattern along curve
Export Extended
Tool	Description
sw_export_step	STEP with AP203/AP214/AP242
sw_export_stl	STL with binary/ASCII + quality


____________


Example Workflows
Create a Simple Box
1. sw_create_part()
2. sw_create_sketch(plane='Front Plane')
3. sw_sketch_rectangle(x1=0, y1=0, x2=100, y2=60)
4. sw_exit_sketch()
5. sw_extrude(depth=40)
6. sw_save_document(file_path='C:/parts/box.sldprt')
7. sw_save_active_document()
Parametric Cylinder
1. sw_create_part()
2. sw_create_sketch(plane='Front Plane')
3. sw_sketch_circle(cx=0, cy=0, radius=25)
4. sw_exit_sketch()
5. sw_extrude(depth=100)
6. sw_add_equation('"Height" = 100mm')
7. sw_add_equation('"D1@Boss-Extrude1" = "Height"')
Assembly with Mates
1. sw_create_assembly()
2. sw_insert_component(file_path='C:/parts/base.sldprt', fixed=True)
3. sw_insert_component(file_path='C:/parts/shaft.sldprt', z=50)
4. sw_add_mate(mate_type='CONCENTRIC', entity1='Face<1>@shaft-1', entity2='Face<1>@base-1')
5. sw_add_mate(mate_type='COINCIDENT', entity1='Face<2>@shaft-1', entity2='Face<2>@base-1')
Sheet Metal Bracket (with bend)
1. sw_create_part()
2. sw_create_sketch(plane='Top Plane')
3. sw_sketch_rectangle(x1=0, y1=0, x2=200, y2=100)
4. sw_exit_sketch()
5. sw_base_flange(thickness=2.0, bend_radius=1.5)
6. sw_create_sketch(plane='<top face>')
7. sw_sketch_line(...)  # bend line
8. sw_exit_sketch()
9. sw_sketched_bend(angle=90)
10. sw_flatten_sheet_metal()
11. sw_export_flat_pattern('C:/parts/bracket_flat.dxf')
Aluminum Part with Custom Color and Mass Analysis
1. sw_create_part()
2. sw_create_sketch('Front Plane')
3. sw_sketch_circle(0, 0, 25)
4. sw_exit_sketch()
5. sw_extrude(depth=100)
6. sw_set_material('Aluminum 6061-T6')
7. sw_set_appearance_color(r=180, g=180, b=200, transparency=0.0)
8. sw_get_mass_properties()  # returns mass, volume, COG
Drawing with Multiple Views + GD&T
1. sw_create_drawing()
2. sw_add_drawing_view(view_type='front', x=100, y=200, scale=1.0)
3. sw_add_projected_view(parent_view_name='Drawing View1', direction='right')
4. sw_add_detail_view(x=80, y=150, radius=15, scale=2.0, label='A')
5. sw_add_centerline(view_name='Drawing View1')
6. sw_add_geometric_tolerance(gtol_text='Position 0.1 A B C', x=50, y=80)
7. sw_add_surface_finish(symbol_type='machining', roughness='3.2')
8. sw_add_bom_table()
9. sw_export_drawing_pdf('C:/drawings/part.pdf')
Parametric Design with Global Variables
1. sw_create_part()
2. sw_set_global_variable(name='Width', value=100)
3. sw_set_global_variable(name='Height', value=60)
4. sw_create_sketch('Front Plane')
5. sw_sketch_rectangle(0, 0, 100, 60)
6. sw_exit_sketch()
7. sw_extrude(depth=40)
8. sw_link_dimension_to_equation(dim_name='D1@Sketch1', expr='"Width"')
9. sw_link_dimension_to_equation(dim_name='D2@Sketch1', expr='"Height"')
# Now changing "Width" or "Height" globals drives the geometry
📝 Notes & Conventions
All length inputs are in millimeters (mm). The server converts internally to meters for the SolidWorks COM API.
All angles are in degrees (converted to radians internally).
SolidWorks must be open (or the server will auto-launch it via sw_connect).
Entity names must match exactly as shown in the SolidWorks FeatureManager tree (case-sensitive).
The server runs as a stdio MCP server (single client, local only — no network exposure).
All COM calls are routed through a single STA thread to satisfy SolidWorks' threading model.