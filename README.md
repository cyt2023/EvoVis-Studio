# EvoVis Studio

EvoVis Studio is a new project for agentic, JSON-driven visualization on the desktop.

It combines:

- a Unity desktop frontend for interactive visualization rendering
- an EvoFlow-style backend runtime for workflow search and execution
- a local backend-service architecture that connects natural-language tasks to visualization results

## Project Structure

- `unity-agentic-vis-pipeline/`
  Unity desktop frontend project. This contains the visualization renderer, Unity-side integration code, scene/runtime controllers, and project logs.

- `OperatorsDraft/`
  EvoFlow-side backend project. This contains workflow search logic, C# operator execution, export generation, backend service scripts, demo datasets, and backend-side notes.

## Core Runtime Direction

The current preferred runtime path is:

`Unity desktop app -> local EvoFlow backend service -> workflow/render JSON -> Unity renderer`

This means Unity acts as the desktop frontend, while the backend is responsible for:

- understanding the task
- selecting or executing a workflow
- producing workflow/render JSON
- returning a result that Unity can visualize

## Current Focus

The current repository focuses on the visualization core and desktop-app integration rather than immersive shell features.

Main current capabilities include:

- OD-oriented visualization workflow execution
- Unity-side backend result rendering
- local backend-service communication
- desktop runtime bootstrap and backend autostart
- structured JSON-based integration between frontend and backend

## Where To Start

If you want to work on the Unity frontend first:

- see `unity-agentic-vis-pipeline/README.md`

If you want to work on the EvoFlow/backend side first:

- see `OperatorsDraft/README.md`

## Notes

This repository is a project-level snapshot that combines the Unity frontend and backend runtime into a single top-level project repository for EvoVis Studio.
