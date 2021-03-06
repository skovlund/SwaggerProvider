﻿namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal.Schema
open Microsoft.FSharp.Quotations
open System
open System.Collections.Generic

/// Object for compiling definitions.
type DefinitionCompiler (schema:SwaggerSchema) =
    let rootType = typeof<System.Collections.Generic.Dictionary<string,obj>>
    let definitions =
        schema.Definitions
        |> Seq.map (fun x -> x.Name, x)
        |> Map.ofSeq
    let compiledTys = System.Collections.Generic.Dictionary<_,_>()

    let generateProperty name ty providedField = 
         ProvidedProperty(nicePascalName name, ty, GetterCode = (fun [this] -> Expr.FieldGet (this, providedField)), 
                                                   SetterCode = (fun [this;v] -> Expr.FieldSet(this, providedField, v)))
    
    let rec compileDefinition name =
        match compiledTys.TryGetValue name with
        | true, ty -> ty
        | false, _ ->
            match definitions.TryFind name with
            | Some(def) ->
                let ty = ProvidedTypeDefinition(nicePascalName def.Name, Some typeof<obj>, IsErased = false)
                ty.AddMemberDelayed(fun () -> ProvidedConstructor([],
                                                        InvokeCode = fun args -> <@@ () @@>))                
                for p in def.Properties do
                    let pTy = compilePropertyType p.Type p.IsRequired
                    let field = ProvidedField("_" + p.Name.ToLower(), pTy)
                    ty.AddMember field
                    let pPr = generateProperty p.Name pTy field
                    if not <| String.IsNullOrWhiteSpace p.Description
                        then pPr.AddXmlDoc p.Description
                    ty.AddMember pPr
                compiledTys.Add(name, ty)
                ty
            | None ->
                failwithf "Unknown definition '%s'" name
    and compilePropertyType ty isRequired =
        match ty, isRequired with
        | Boolean, true   -> typeof<bool>
        | Boolean, false  -> typeof<Option<bool>>
        | Int32, true     -> typeof<int32>
        | Int32, false    -> typeof<Option<int32>>
        | Int64, true     -> typeof<int64>
        | Int64, false    -> typeof<Option<int64>>
        | Float, true     -> typeof<float>
        | Float, false    -> typeof<Option<float>>
        | Double, true    -> typeof<double>
        | Double, false   -> typeof<Option<double>>
        | String, _       -> typeof<string>
        | Date, true | DateTime, true   -> typeof<DateTime>
        | Date, false | DateTime, false -> typeof<Option<DateTime>>
        | Enum vals, _ -> typeof<string> //TODO: find better type
        | Array iTy, _ -> (compilePropertyType iTy true).MakeArrayType()
//        | Array iTy, _ -> (compilePropertyType iTy true).MakeArrayType(1)
        | Dictionary eTy, _ -> typedefof<Map<string, obj>>.MakeGenericType([|typeof<string>; compilePropertyType eTy true|])
        | Definition name, _ -> compileDefinition name :> Type //TODO: make types nullable
        | File, _ -> typeof<byte>.MakeArrayType(1)

    /// Compiles the definition.
    member __.Compile() =
        let root = ProvidedTypeDefinition("Definitions", Some typeof<obj>, IsErased = false)
        schema.Definitions |> Seq.iter (fun def ->
            compileDefinition def.Name |> ignore)
        for pTy in compiledTys.Values do
            root.AddMember pTy
        root
    /// Compiles the definition.
    member __.CompileTy ty required =
        compilePropertyType ty required

